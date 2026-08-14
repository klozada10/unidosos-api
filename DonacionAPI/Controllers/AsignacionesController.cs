using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DonacionAPI.Data;
using DonacionAPI.DTOs;
using DonacionAPI.Models;

namespace DonacionAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // ← SOLO voluntarios logueados pueden acceder
public class AsignacionesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AsignacionesController(AppDbContext context)
    {
        _context = context;
    }

    // ────────────────────────────────────────────────────────
    // PANEL PRINCIPAL: Lista de solicitudes de ayuda (PRIVADO)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Lista TODAS las solicitudes de ayuda.
    /// Solo voluntarios autenticados pueden ver esto.
    /// Incluye cuántas veces fue atendida cada persona y si el voluntario actual la tiene asignada.
    /// </summary>
    [HttpGet("solicitudes")]
    public async Task<ActionResult<IEnumerable<SolicitudAdminDto>>> GetSolicitudes(
        [FromQuery] string filtro = "todas",   // todas | sin_atender | asignadas_a_mi | entregadas
        [FromQuery] string? buscarNumero = null)
    {
        int miVoluntarioId = ObtenerVoluntarioId();

        var query = _context.SolicitudesAyuda
            .Where(s => s.Activo)
            .Include(s => s.Fotos)
            .AsQueryable();

        // Buscar por número (#15, #500, etc.)
        if (!string.IsNullOrEmpty(buscarNumero) && int.TryParse(buscarNumero.Replace("#", ""), out int numBuscado))
        {
            query = query.Where(s => s.Id == numBuscado);
        }

        var solicitudes = await query
            .OrderBy(s => s.Id) // Orden incremental siempre
            .ToListAsync();

        // Cargar asignaciones en batch
        var solicitudIds = solicitudes.Select(s => s.Id).ToList();
        var todasAsignaciones = await _context.AsignacionesAyuda
            .Where(a => solicitudIds.Contains(a.SolicitudId))
            .Include(a => a.Voluntario)
            .ToListAsync();

        var resultado = solicitudes.Select(s =>
        {
            var asignacionesDeSolicitud = todasAsignaciones.Where(a => a.SolicitudId == s.Id).ToList();
            var entregadasDeSolicitud = asignacionesDeSolicitud.Where(a => a.Estado == "entregado").ToList();
            var miAsignacion = asignacionesDeSolicitud.FirstOrDefault(a => a.VoluntarioId == miVoluntarioId);

            return new SolicitudAdminDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Apellido = s.Apellido,
                Celular = s.Celular,
                Correo = s.Correo,
                Direccion = s.Direccion,
                Barrio = s.Barrio,
                Ciudad = s.Ciudad,
                DescripcionNecesidad = s.DescripcionNecesidad,
                Estado = s.Estado,
                UrlFotos = s.Fotos.Select(f => f.UrlFoto).ToList(),
                CreatedAt = s.CreatedAt,
                TotalVecesAtendida = entregadasDeSolicitud.Count,
                TotalAsignados = asignacionesDeSolicitud.Count(a => a.Estado == "asignado"),
                YoLaTengoAsignada = miAsignacion?.Estado == "asignado",
                YoLaEntregue = miAsignacion?.Estado == "entregado",
                Asignaciones = asignacionesDeSolicitud.Select(a => new AsignacionResumenDto
                {
                    Id = a.Id,
                    NombreVoluntario = $"{a.Voluntario.Nombre} {a.Voluntario.Apellido}",
                    ApodoVoluntario = a.Voluntario.Apodo,
                    Estado = a.Estado,
                    FechaAsignacion = a.FechaAsignacion,
                    FechaEntrega = a.FechaEntrega,
                    Notas = a.Notas
                }).ToList()
            };
        });

        // Aplicar filtro post-mapeo
        resultado = filtro switch
        {
            "sin_atender" => resultado.Where(s => s.TotalVecesAtendida == 0),
            "asignadas_a_mi" => resultado.Where(s => s.YoLaTengoAsignada),
            "entregadas" => resultado.Where(s => s.YoLaEntregue),
            _ => resultado
        };

        return Ok(resultado.ToList());
    }

    /// <summary>
    /// Ver una solicitud específica por número (#15)
    /// </summary>
    [HttpGet("solicitudes/{numero}")]
    public async Task<ActionResult<SolicitudAdminDto>> GetSolicitud(int numero)
    {
        int miVoluntarioId = ObtenerVoluntarioId();

        var solicitud = await _context.SolicitudesAyuda
            .Where(s => s.Id == numero && s.Activo)
            .Include(s => s.Fotos)
            .FirstOrDefaultAsync();

        if (solicitud == null) return NotFound(new { mensaje = $"No existe la solicitud #{numero}" });

        var asignaciones = await _context.AsignacionesAyuda
            .Where(a => a.SolicitudId == numero)
            .Include(a => a.Voluntario)
            .ToListAsync();

        var miAsignacion = asignaciones.FirstOrDefault(a => a.VoluntarioId == miVoluntarioId);

        return Ok(new SolicitudAdminDto
        {
            Id = solicitud.Id,
            Nombre = solicitud.Nombre,
            Apellido = solicitud.Apellido,
            Celular = solicitud.Celular,
            Correo = solicitud.Correo,
            Direccion = solicitud.Direccion,
            Barrio = solicitud.Barrio,
            Ciudad = solicitud.Ciudad,
            DescripcionNecesidad = solicitud.DescripcionNecesidad,
            Estado = solicitud.Estado,
            UrlFotos = solicitud.Fotos.Select(f => f.UrlFoto).ToList(),
            CreatedAt = solicitud.CreatedAt,
            TotalVecesAtendida = asignaciones.Count(a => a.Estado == "entregado"),
            TotalAsignados = asignaciones.Count(a => a.Estado == "asignado"),
            YoLaTengoAsignada = miAsignacion?.Estado == "asignado",
            YoLaEntregue = miAsignacion?.Estado == "entregado",
            Asignaciones = asignaciones.Select(a => new AsignacionResumenDto
            {
                Id = a.Id,
                NombreVoluntario = $"{a.Voluntario.Nombre} {a.Voluntario.Apellido}",
                ApodoVoluntario = a.Voluntario.Apodo,
                Estado = a.Estado,
                FechaAsignacion = a.FechaAsignacion,
                FechaEntrega = a.FechaEntrega,
                Notas = a.Notas
            }).ToList()
        });
    }

    // ────────────────────────────────────────────────────────
    // ASIGNAR: "Yo quiero ayudar al #15"
    // ────────────────────────────────────────────────────────

    [HttpPost("asignar")]
    public async Task<ActionResult> Asignar([FromBody] AsignarAyudaDto dto)
    {
        int miVoluntarioId = ObtenerVoluntarioId();

        var solicitud = await _context.SolicitudesAyuda.FindAsync(dto.SolicitudId);
        if (solicitud == null || !solicitud.Activo)
            return NotFound(new { mensaje = $"No existe la solicitud #{dto.SolicitudId}" });

        // ¿Ya la tengo asignada?
        var yaAsignada = await _context.AsignacionesAyuda.AnyAsync(a =>
            a.SolicitudId == dto.SolicitudId && a.VoluntarioId == miVoluntarioId);

        if (yaAsignada)
            return BadRequest(new { mensaje = "Ya tienes asignada esta solicitud" });

        var asignacion = new AsignacionAyuda
        {
            SolicitudId = dto.SolicitudId,
            VoluntarioId = miVoluntarioId,
            Estado = "asignado",
            Notas = dto.Notas
        };

        _context.AsignacionesAyuda.Add(asignacion);
        await _context.SaveChangesAsync();

        var miNombre = User.FindFirst("Apodo")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return Ok(new { mensaje = $"✅ {miNombre} quedó asignado a la solicitud #{dto.SolicitudId}" });
    }

    // ────────────────────────────────────────────────────────
    // MARCAR ENTREGADO: check ✓ "Ya le entregué ayuda al #15"
    // ────────────────────────────────────────────────────────

    [HttpPut("entregar/{asignacionId}")]
    public async Task<ActionResult> MarcarEntregado(int asignacionId, [FromBody] MarcarEntregadoDto dto)
    {
        int miVoluntarioId = ObtenerVoluntarioId();

        var asignacion = await _context.AsignacionesAyuda
            .FirstOrDefaultAsync(a => a.Id == asignacionId && a.VoluntarioId == miVoluntarioId);

        if (asignacion == null)
            return NotFound(new { mensaje = "Asignación no encontrada o no te pertenece" });

        if (asignacion.Estado == "entregado")
            return BadRequest(new { mensaje = "Esta entrega ya fue marcada como completada" });

        asignacion.Estado = "entregado";
        asignacion.FechaEntrega = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Notas))
            asignacion.Notas = dto.Notas;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje = $"✅ ¡Entrega marcada! Solicitud #{asignacion.SolicitudId} atendida.",
            solicitudId = asignacion.SolicitudId
        });
    }

    // ────────────────────────────────────────────────────────
    // CANCELAR ASIGNACIÓN (si un voluntario ya no puede ir)
    // ────────────────────────────────────────────────────────

    [HttpDelete("cancelar/{asignacionId}")]
    public async Task<ActionResult> CancelarAsignacion(int asignacionId)
    {
        int miVoluntarioId = ObtenerVoluntarioId();

        var asignacion = await _context.AsignacionesAyuda
            .FirstOrDefaultAsync(a => a.Id == asignacionId && a.VoluntarioId == miVoluntarioId);

        if (asignacion == null) return NotFound();
        if (asignacion.Estado == "entregado")
            return BadRequest(new { mensaje = "No puedes cancelar una entrega ya completada" });

        _context.AsignacionesAyuda.Remove(asignacion);
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Asignación cancelada" });
    }

    // ────────────────────────────────────────────────────────
    // RESUMEN ESTADÍSTICO para el panel del voluntario
    // ────────────────────────────────────────────────────────

    [HttpGet("resumen")]
    public async Task<ActionResult> GetResumen()
    {
        int miVoluntarioId = ObtenerVoluntarioId();

        var totalSolicitudes = await _context.SolicitudesAyuda.CountAsync(s => s.Activo);
        var totalSinAtender = await _context.SolicitudesAyuda
            .Where(s => s.Activo)
            .CountAsync(s => !_context.AsignacionesAyuda.Any(a => a.SolicitudId == s.Id && a.Estado == "entregado"));
        var misAsignaciones = await _context.AsignacionesAyuda
            .CountAsync(a => a.VoluntarioId == miVoluntarioId && a.Estado == "asignado");
        var misEntregas = await _context.AsignacionesAyuda
            .CountAsync(a => a.VoluntarioId == miVoluntarioId && a.Estado == "entregado");

        return Ok(new
        {
            totalSolicitudes,
            totalSinAtender,
            misAsignaciones,
            misEntregas
        });
    }

    // ────────────────────────────────────────────────────────
    private int ObtenerVoluntarioId()
    {
        var claim = User.FindFirst("VoluntarioId")?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
