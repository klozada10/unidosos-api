using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DonacionAPI.Data;
using DonacionAPI.DTOs;

namespace DonacionAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    /// <summary>
    /// Login para voluntarios/influencers.
    /// Retorna un JWT que deben incluir en el header Authorization: Bearer {token}
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto dto)
    {
        var acceso = await _context.VoluntariosAcceso
            .Include(a => a.Voluntario)
                .ThenInclude(v => v.PuntoAcopio)
            .FirstOrDefaultAsync(a => a.Username == dto.Username.ToLower() && a.Activo);

        if (acceso == null)
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos" });

        // Verificar contraseña con BCrypt
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, acceso.PasswordHash))
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos" });

        // Actualizar último acceso
        acceso.UltimoAcceso = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generar JWT
        var token = GenerarToken(acceso);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = acceso.Username,
            NombreCompleto = $"{acceso.Voluntario.Nombre} {acceso.Voluntario.Apellido}",
            Apodo = acceso.Voluntario.Apodo,
            EsSuperAdmin = acceso.EsSuperAdmin,
            VoluntarioId = acceso.VoluntarioId,
            PuntoAcopioId = acceso.Voluntario.PuntoAcopioId,
            NombrePunto = acceso.Voluntario.PuntoAcopio?.Nombre ?? "",
            Expiracion = DateTime.UtcNow.AddDays(7)
        });
    }

    /// <summary>
    /// Configurar/cambiar contraseña de un usuario la primera vez.
    /// Requiere un código admin definido en appsettings.json
    /// </summary>
    [HttpPost("setup-password")]
    public async Task<ActionResult> SetupPassword([FromBody] SetupPasswordDto dto)
    {
        var codigoEsperado = _config["Auth:SetupCode"];
        if (string.IsNullOrEmpty(codigoEsperado) || dto.CodigoAdmin != codigoEsperado)
            return Unauthorized(new { mensaje = "Código de administración incorrecto" });

        var acceso = await _context.VoluntariosAcceso
            .FirstOrDefaultAsync(a => a.Username == dto.Username.ToLower());

        if (acceso == null)
            return NotFound(new { mensaje = $"Usuario '{dto.Username}' no encontrado" });

        if (dto.NuevaPassword.Length < 8)
            return BadRequest(new { mensaje = "La contraseña debe tener mínimo 8 caracteres" });

        acceso.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword, workFactor: 11);
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = $"Contraseña de '{dto.Username}' actualizada exitosamente" });
    }

    /// <summary>
    /// Registro de nuevo voluntario con su propia contraseña
    /// </summary>
    [HttpPost("registro")]
    public async Task<ActionResult<LoginResponseDto>> RegistrarVoluntario([FromBody] RegistroVoluntarioDto dto)
    {
        // Validaciones básicas
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Apellido))
            return BadRequest(new { mensaje = "Nombre y apellido son obligatorios" });

        if (dto.Password.Length < 8)
            return BadRequest(new { mensaje = "La contraseña debe tener mínimo 8 caracteres" });

        var username = dto.Username.ToLower().Trim();
        if (username.Length < 3)
            return BadRequest(new { mensaje = "El nombre de usuario debe tener mínimo 3 caracteres" });

        // Verificar que el punto de acopio existe
        var punto = await _context.PuntosAcopio.FindAsync(dto.PuntoAcopioId);
        if (punto == null)
            return BadRequest(new { mensaje = "Punto de acopio no válido" });

        // Verificar que el username no esté tomado
        var existe = await _context.VoluntariosAcceso.AnyAsync(a => a.Username == username);
        if (existe)
            return Conflict(new { mensaje = $"El usuario '{username}' ya existe. Elige otro nombre de usuario." });

        // Crear voluntario
        var voluntario = new Models.Voluntario
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Apodo = string.IsNullOrWhiteSpace(dto.Apodo) ? dto.Nombre.Trim() : dto.Apodo.Trim(),
            Telefono = dto.Telefono?.Trim(),
            PuntoAcopioId = dto.PuntoAcopioId,
            Activo = true
        };
        _context.Voluntarios.Add(voluntario);
        await _context.SaveChangesAsync();

        // Crear acceso
        var acceso = new Models.VoluntarioAcceso
        {
            VoluntarioId = voluntario.Id,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11),
            EsSuperAdmin = false,
            Activo = true
        };
        _context.VoluntariosAcceso.Add(acceso);
        await _context.SaveChangesAsync();

        // Cargar relaciones para el token
        acceso.Voluntario = voluntario;
        voluntario.PuntoAcopio = punto;

        var token = GenerarToken(acceso);
        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = acceso.Username,
            NombreCompleto = $"{voluntario.Nombre} {voluntario.Apellido}",
            Apodo = voluntario.Apodo,
            EsSuperAdmin = false,
            VoluntarioId = voluntario.Id,
            PuntoAcopioId = voluntario.PuntoAcopioId,
            NombrePunto = punto.Nombre,
            Expiracion = DateTime.UtcNow.AddDays(7)
        });
    }

    /// <summary>
    /// Verificar que el token aún es válido (para el frontend al recargar)
    /// </summary>
    [HttpGet("verify")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public ActionResult Verify()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var nombreCompleto = User.FindFirst("NombreCompleto")?.Value;
        return Ok(new { valido = true, username, nombreCompleto });
    }

    // ───────────────────────────────────────────────────────
    private string GenerarToken(Models.VoluntarioAcceso acceso)
    {
        var jwtKey = _config["Auth:JwtKey"] ?? throw new InvalidOperationException("JWT Key no configurada");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, acceso.Id.ToString()),
            new Claim(ClaimTypes.Name, acceso.Username),
            new Claim("VoluntarioId", acceso.VoluntarioId.ToString()),
            new Claim("PuntoAcopioId", acceso.Voluntario.PuntoAcopioId.ToString()),
            new Claim("NombreCompleto", $"{acceso.Voluntario.Nombre} {acceso.Voluntario.Apellido}"),
            new Claim("Apodo", acceso.Voluntario.Apodo ?? acceso.Voluntario.Nombre),
            new Claim("EsSuperAdmin", acceso.EsSuperAdmin.ToString()),
            new Claim(ClaimTypes.Role, acceso.EsSuperAdmin ? "SuperAdmin" : "Voluntario")
        };

        var token = new JwtSecurityToken(
            issuer: "UNIDOSOS",
            audience: "UNIDOSOS-App",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
