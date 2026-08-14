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

    public PuntosAcopioController(AppDbContext context)
    {
        _context = context;
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
        Voluntarios = p.Voluntarios.Select(v => new VoluntarioDto
        {
            Id = v.Id,
            Nombre = v.Nombre,
            Apellido = v.Apellido,
            Apodo = v.Apodo,
            Telefono = v.Telefono
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
