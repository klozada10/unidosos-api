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

    public PuntosAcopioController(
        AppDbContext context,
        IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetPuntos()
    {
        try
        {
            var puntos = await _context.PuntosAcopio
                .Where(p => p.Activo)
                .Include(p => p.Voluntarios)
                .Include(p => p.ItemsNecesarios)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return Ok(puntos.Select(MapToDto));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                mensaje = ex.Message,
                detalle = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        try
        {
            var total = await _context.PuntosAcopio.CountAsync();

            return Ok(new
            {
                total
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                mensaje = ex.Message,
                detalle = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPunto(int id)
    {
        try
        {
            var punto = await _context.PuntosAcopio
                .Where(p => p.Id == id && p.Activo)
                .Include(p => p.Voluntarios)
                .Include(p => p.ItemsNecesarios)
                .FirstOrDefaultAsync();

            if (punto == null)
            {
                return NotFound(new
                {
                    mensaje = "Punto de acopio no encontrado"
                });
            }

            return Ok(MapToDto(punto));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                mensaje = ex.Message,
                detalle = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PuntoAcopioDto>> CreatePunto(
        [FromBody] CreatePuntoAcopioDto dto)
    {
        try
        {
            var punto = new PuntoAcopio
            {
                Nombre = dto.Nombre,
                Direccion = dto.Direccion,
                Barrio = dto.Barrio,
                Ciudad = dto.Ciudad,
                HorarioInicio = TimeSpan.Parse(dto.HorarioInicio),
                HorarioFin = TimeSpan.Parse(dto.HorarioFin),
                Descripcion = dto.Descripcion,
                Telefono = dto.Telefono
            };

            _context.PuntosAcopio.Add(punto);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetPunto),
                new { id = punto.Id },
                MapToDto(punto));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                mensaje = ex.Message,
                detalle = ex.InnerException?.Message
            });
        }
    }

    private static PuntoAcopioDto MapToDto(PuntoAcopio p) => new()
    {
        Id = p.Id,
        Nombre = p.Nombre,
        Direccion = p.Direccion,
        Barrio = p.Barrio,
        Ciudad = p.Ciudad,
        HorarioInicio = p.HorarioInicio.ToString(@"hh\:mm"),
        HorarioFin = p.HorarioFin.ToString(@"hh\:mm"),
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
            .Select(i => new ItemNecesarioDto
            {
                Id = i.Id,
                Nombre = i.Nombre,
                Descripcion = i.Descripcion,
                Prioridad = i.Prioridad
            })
            .ToList()
    };
}