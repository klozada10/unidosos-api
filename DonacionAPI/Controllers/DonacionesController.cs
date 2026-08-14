using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DonacionAPI.Data;
using DonacionAPI.DTOs;
using DonacionAPI.Models;

namespace DonacionAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DonacionesController : ControllerBase
{
    private readonly AppDbContext _context;

    public DonacionesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el inventario completo de un punto de acopio, ordenado alfabéticamente
    /// </summary>
    [HttpGet("inventario/{puntoAcopioId}")]
    public async Task<ActionResult<IEnumerable<InventarioItemDto>>> GetInventario(
        int puntoAcopioId,
        [FromQuery] string? buscar = null)
    {
        var query = _context.InventarioDonaciones
            .Where(i => i.PuntoAcopioId == puntoAcopioId);

        // Búsqueda opcional por nombre
        if (!string.IsNullOrWhiteSpace(buscar))
            query = query.Where(i => i.NombreItem.Contains(buscar));

        var items = await query
            .OrderBy(i => i.NombreItem) // Orden alfabético
            .Select(i => new InventarioItemDto
            {
                Id = i.Id,
                NombreItem = i.NombreItem,
                Unidad = i.Unidad,
                CantidadTotal = i.CantidadTotal,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Busca si ya existe un ítem en el inventario (para autocompletar)
    /// </summary>
    [HttpGet("inventario/{puntoAcopioId}/buscar")]
    public async Task<ActionResult<IEnumerable<string>>> BuscarItems(
        int puntoAcopioId,
        [FromQuery] string termino = "")
    {
        var items = await _context.InventarioDonaciones
            .Where(i => i.PuntoAcopioId == puntoAcopioId && i.NombreItem.Contains(termino))
            .OrderBy(i => i.NombreItem)
            .Select(i => i.NombreItem)
            .Take(10)
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Registra una donación recibida.
    /// Si el ítem ya existe en el inventario, SUMA la cantidad.
    /// Si no existe, lo crea como nuevo ítem.
    /// </summary>
    [HttpPost("registrar")]
    public async Task<ActionResult<InventarioItemDto>> RegistrarDonacion([FromBody] AgregarDonacionDto dto)
    {
        // Validar que el punto existe
        var puntoExiste = await _context.PuntosAcopio.AnyAsync(p => p.Id == dto.PuntoAcopioId && p.Activo);
        if (!puntoExiste) return NotFound("Punto de acopio no encontrado");

        // Normalizar el nombre del ítem (trim + capitalizar primera letra)
        var nombreNormalizado = dto.NombreItem.Trim();

        // Buscar si ya existe el ítem (búsqueda case-insensitive)
        var itemExistente = await _context.InventarioDonaciones
            .FirstOrDefaultAsync(i =>
                i.PuntoAcopioId == dto.PuntoAcopioId &&
                i.NombreItem.ToLower() == nombreNormalizado.ToLower());

        InventarioDonacion item;

        if (itemExistente != null)
        {
            // ✅ Ya existe: SUMAR la cantidad
            itemExistente.CantidadTotal += dto.Cantidad;
            itemExistente.UpdatedAt = DateTime.UtcNow;
            item = itemExistente;
        }
        else
        {
            // ✅ No existe: CREAR nuevo ítem
            item = new InventarioDonacion
            {
                PuntoAcopioId = dto.PuntoAcopioId,
                NombreItem = nombreNormalizado,
                Unidad = dto.Unidad,
                CantidadTotal = dto.Cantidad
            };
            _context.InventarioDonaciones.Add(item);
        }

        // Registrar el movimiento para trazabilidad
        var movimiento = new MovimientoDonacion
        {
            Inventario = item,
            Cantidad = dto.Cantidad,
            NombreDonante = dto.NombreDonante,
            Observacion = dto.Observacion,
            FechaRecepcion = DateTime.UtcNow
        };
        _context.MovimientosDonacion.Add(movimiento);

        await _context.SaveChangesAsync();

        return Ok(new InventarioItemDto
        {
            Id = item.Id,
            NombreItem = item.NombreItem,
            Unidad = item.Unidad,
            CantidadTotal = item.CantidadTotal,
            UpdatedAt = item.UpdatedAt
        });
    }
}
