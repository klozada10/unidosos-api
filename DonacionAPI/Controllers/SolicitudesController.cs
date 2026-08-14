using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DonacionAPI.Data;
using DonacionAPI.DTOs;
using DonacionAPI.Models;

namespace DonacionAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolicitudesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public SolicitudesController(AppDbContext context, IWebHostEnvironment env, IConfiguration config)
    {
        _context = context;
        _env = env;
        _config = config;
    }

    /// <summary>
    /// Lista todas las solicitudes de ayuda pendientes
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SolicitudAyudaDto>>> GetSolicitudes(
        [FromQuery] string estado = "pendiente")
    {
        var solicitudes = await _context.SolicitudesAyuda
            .Where(s => s.Activo && s.Estado == estado)
            .Include(s => s.Fotos)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(solicitudes.Select(MapToDto));
    }

    /// <summary>
    /// Obtiene una solicitud específica
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SolicitudAyudaDto>> GetSolicitud(int id)
    {
        var solicitud = await _context.SolicitudesAyuda
            .Where(s => s.Id == id && s.Activo)
            .Include(s => s.Fotos)
            .FirstOrDefaultAsync();

        if (solicitud == null) return NotFound();

        return Ok(MapToDto(solicitud));
    }

    /// <summary>
    /// Registra una nueva solicitud de ayuda (sin fotos aún)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SolicitudAyudaDto>> CreateSolicitud([FromBody] CreateSolicitudDto dto)
    {
        var solicitud = new SolicitudAyuda
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Celular = dto.Celular.Trim(),
            Correo = dto.Correo?.Trim(),
            Direccion = dto.Direccion.Trim(),
            Barrio = dto.Barrio?.Trim(),
            Ciudad = dto.Ciudad.Trim(),
            DescripcionNecesidad = dto.DescripcionNecesidad.Trim()
        };

        _context.SolicitudesAyuda.Add(solicitud);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSolicitud), new { id = solicitud.Id }, MapToDto(solicitud));
    }

    /// <summary>
    /// Sube fotos para una solicitud (máximo 5)
    /// Llamar después de crear la solicitud
    /// </summary>
    [HttpPost("{id}/fotos")]
    public async Task<ActionResult> SubirFotos(int id, [List<IFormFile>] IFormFileCollection fotos)
    {
        var solicitud = await _context.SolicitudesAyuda
            .Include(s => s.Fotos)
            .FirstOrDefaultAsync(s => s.Id == id && s.Activo);

        if (solicitud == null) return NotFound("Solicitud no encontrada");

        int maxFotos = _config.GetValue<int>("FileStorage:MaxPhotosPerSolicitud", 5);
        int fotosActuales = solicitud.Fotos.Count;

        if (fotosActuales + fotos.Count > maxFotos)
            return BadRequest($"Se permiten máximo {maxFotos} fotos. Ya tiene {fotosActuales}.");

        long maxSizeMB = _config.GetValue<long>("FileStorage:MaxFileSizeMB", 5);
        long maxSizeBytes = maxSizeMB * 1024 * 1024;

        var uploadPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "solicitudes", id.ToString());
        Directory.CreateDirectory(uploadPath);

        var urlsSubidas = new List<string>();

        foreach (var foto in fotos)
        {
            if (foto.Length > maxSizeBytes)
                return BadRequest($"El archivo {foto.FileName} supera el límite de {maxSizeMB}MB");

            var extension = Path.GetExtension(foto.FileName).ToLower();
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
                return BadRequest("Solo se aceptan imágenes JPG, PNG o WEBP");

            var nombreArchivo = $"{Guid.NewGuid()}{extension}";
            var rutaCompleta = Path.Combine(uploadPath, nombreArchivo);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                await foto.CopyToAsync(stream);

            var urlFoto = $"/uploads/solicitudes/{id}/{nombreArchivo}";
            urlsSubidas.Add(urlFoto);

            solicitud.Fotos.Add(new FotoSolicitud
            {
                SolicitudId = id,
                UrlFoto = urlFoto,
                NombreArchivo = foto.FileName
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new { fotos = urlsSubidas, mensaje = $"{fotos.Count} foto(s) subida(s) exitosamente" });
    }

    private static SolicitudAyudaDto MapToDto(SolicitudAyuda s) => new()
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
        CreatedAt = s.CreatedAt
    };
}
