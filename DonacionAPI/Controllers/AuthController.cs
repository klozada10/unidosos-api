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
