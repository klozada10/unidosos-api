using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DonacionAPI.Data;
using DonacionAPI.DTOs;
using DonacionAPI.Models;

namespace DonacionAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PuntosAcopioController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public PuntosAcopioController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    /// <summary>
    /// Obtiene todos los puntos de acopio activos con sus voluntarios e ítems necesarios
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PuntoAcopioDto>>> GetPuntos()
    {
        var puntos = await _context.PuntosAcopio
            .Where(p => p.Activo)
            .Include(p => p.Voluntarios.Where(v => v.Activo))
            .Include(p => p.ItemsNecesarios.Where(i => i.Activo))
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        return Ok(puntos.Select(MapToDto));
    }

    /// <summary>
    /// Obtiene un punto de acopio específico con su inventario actual
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PuntoAcopioDto>> GetPunto(int id)
    {
        var punto = await _context.PuntosAcopio
            .Where(p => p.Id == id && p.Activo)
            .Include(p => p.Voluntarios.Where(v => v.Activo))
            .Include(p => p.ItemsNecesarios.Where(i => i.Activo))
            .FirstOrDefaultAsync();

        if (punto == null) return NotFound("Punto de acopio no encontrado");

        return Ok(MapToDto(punto));
    }

    /// <summary>
    /// Crea un nuevo punto de acopio
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PuntoAcopioDto>> CreatePunto([FromBody] CreatePuntoAcopioDto dto)
    {
        var punto = new PuntoAcopio
        {
            Nombre = dto.Nombre,
            Direccion = dto.Direccion,
            Barrio = dto.Barrio,
            Ciudad = dto.Ciudad,
            HorarioInicio = TimeOnly.Parse(dto.HorarioInicio),
            HorarioFin = TimeOnly.Parse(dto.HorarioFin),
            Descripcion = dto.Descripcion,
            Telefono = dto.Telefono
        };

        _context.PuntosAcopio.Add(punto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPunto), new { id = punto.Id }, MapToDto(punto));
    }

    /// <summary>
    /// Busca un punto de acopio por el código del influencer (ej: "001")
    /// </summary>
    [HttpGet("buscar")]
    public async Task<ActionResult<PuntoAcopioDto>> BuscarPorCodigo([FromQuery] string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return BadRequest(new { mensaje = "Debes ingresar un código" });

        var voluntario = await _context.Voluntarios
            .Where(v => v.Codigo == codigo.Trim() && v.Activo && v.PuntoAcopioId != null)
            .FirstOrDefaultAsync();

        if (voluntario == null)
            return NotFound(new { mensaje = $"No encontramos ningún punto con el código \"{codigo}\"" });

        var punto = await _context.PuntosAcopio
            .Where(p => p.Id == voluntario.PuntoAcopioId && p.Activo)
            .Include(p => p.Voluntarios.Where(v => v.Activo))
            .Include(p => p.ItemsNecesarios.Where(i => i.Activo))
            .FirstOrDefaultAsync();

        if (punto == null)
            return NotFound(new { mensaje = "El punto de acopio no está disponible actualmente" });

        return Ok(MapToDto(punto));
    }

    /// <summary>
    /// Sube o reemplaza el flyer de un punto de acopio
    /// </summary>
    [HttpPost("{id}/flyer")]
    public async Task<ActionResult> SubirFlyer(int id, IFormFile flyer)
    {
        var punto = await _context.PuntosAcopio.FindAsync(id);
        if (punto == null) return NotFound("Punto de acopio no encontrado");

        var extension = Path.GetExtension(flyer.FileName).ToLower();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
            return BadRequest("Solo se aceptan imágenes JPG, PNG o WEBP");

        if (flyer.Length > 8 * 1024 * 1024)
            return BadRequest("La imagen no debe superar 8 MB");

        var uploadPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "puntos");
        Directory.CreateDirectory(uploadPath);

        var nombreArchivo = $"flyer_{id}_{Guid.NewGuid()}{extension}";
        var rutaCompleta = Path.Combine(uploadPath, nombreArchivo);

        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            await flyer.CopyToAsync(stream);

        punto.UrlFlyer = $"/uploads/puntos/{nombreArchivo}";
        punto.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { urlFlyer = punto.UrlFlyer, mensaje = "Flyer subido correctamente" });
    }

    /// <summary>
    /// Agrega un voluntario a un punto de acopio
    /// </summary>
    [HttpPost("{id}/voluntarios")]
    public async Task<ActionResult> AddVoluntario(int id, [FromBody] VoluntarioDto dto)
    {
        var punto = await _context.PuntosAcopio.FindAsync(id);
        if (punto == null) return NotFound();

        var voluntario = new Voluntario
        {
            PuntoAcopioId = id,
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Apodo = dto.Apodo,
            Telefono = dto.Telefono
        };

        _context.Voluntarios.Add(voluntario);
        await _context.SaveChangesAsync();

        return Ok(voluntario);
    }

    /// <summary>
    /// Agrega un ítem necesario a un punto de acopio
    /// </summary>
    [HttpPost("{id}/items-necesarios")]
    public async Task<ActionResult> AddItemNecesario(int id, [FromBody] ItemNecesarioDto dto)
    {
        var punto = await _context.PuntosAcopio.FindAsync(id);
        if (punto == null) return NotFound();

        var item = new ItemNecesario
        {
            PuntoAcopioId = id,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Prioridad = dto.Prioridad
        };

        _context.ItemsNecesarios.Add(item);
        await _context.SaveChangesAsync();

        return Ok(item);
    }

    private static PuntoAcopioDto MapToDto(PuntoAcopio p) => new()
    {
        Id = p.Id,
        Nombre = p.Nombre,
        Direccion = p.Direccion,
        Barrio = p.Barrio,
        Ciudad = p.Ciudad,
        HorarioInicio = p.HorarioInicio.ToString("HH:mm"),
        HorarioFin = p.HorarioFin.ToString("HH:mm"),
        Descripcion = p.Descripcion,
        Telefono = p.Telefono,
        UrlFlyer = p.UrlFlyer,
        Voluntarios = p.Voluntarios.Select(v => new VoluntarioDto
        {
            Id = v.Id,
            Nombre = v.Nombre,
            Apellido = v.Apellido,
            Apodo = v.Apodo,
            Telefono = v.Telefono,
            Codigo = v.Codigo
        }).ToList(),
        ItemsNecesarios = p.ItemsNecesarios
            .OrderBy(i => i.Prioridad == "alta" ? 0 : i.Prioridad == "media" ? 1 : 2)
            .Select(i => new ItemNecesarioDto
            {
                Id = i.Id,
                Nombre = i.Nombre,
                Descripcion = i.Descripcion,
                Prioridad = i.Prioridad
            }).ToList()
    };
}
