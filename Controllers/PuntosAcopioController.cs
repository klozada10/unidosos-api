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

    /// <summary>
    /// Obtiene todos los puntos de acopio activos
    /// </summary>
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

    /// <summary>
    /// Endpoint para probar conexión a BD
    /// </summary>
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
    [HttpGet("ping-2026")]
public IActionResult Ping2026()
{
    return Ok("PING FUNCIONA");
}

    /// <summary>
    /// Obtiene un punto por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PuntoAcopioDto>> GetPunto(int id)
    {
        try
        {
            var punto = await _context.PuntosAcopio
                .Where(p => p.Id == id && p.Activo)
                .Include(p => p.Voluntarios)
                .Include(p => p.ItemsNecesarios)
                .FirstOrDefaultAsync();

            if (punto == null)
                return NotFound(new
                {
                    mensaje = "Punto de acopio no encontrado"
                });

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
    
}