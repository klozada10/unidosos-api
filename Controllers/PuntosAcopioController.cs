using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DonacionAPI.Data;
using DonacionAPI.DTOs;
using DonacionAPI.Models;
using DonacionAPI.Services;

namespace DonacionAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PuntosAcopioController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IImagenService _imagenes;
    private readonly IConfiguration _config;
    private readonly ILogger<PuntosAcopioController> _logger;

    private static readonly string[] TiposImagenPermitidos =
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    public PuntosAcopioController(
        AppDbContext context,
        IImagenService imagenes,
        IConfiguration config,
        ILogger<PuntosAcopioController> logger)
    {
        _context = context;
        _imagenes = imagenes;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Lista los puntos activos ordenados alfabeticamente por ciudad y, dentro
    /// de cada ciudad, por nombre. El frontend pinta la lista en el orden que
    /// recibe, asi que el orden se decide aqui.
    /// Admite ?ciudad=Cali para filtrar por una ciudad concreta.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPuntos([FromQuery] string? ciudad = null)
    {
        try
        {
            var query = _context.PuntosAcopio.Where(p => p.Activo);

            if (!string.IsNullOrWhiteSpace(ciudad))
            {
                var filtro = ciudad.Trim();
                query = query.Where(p => p.Ciudad == filtro);
            }

            var puntos = await query
                .Include(p => p.Voluntarios)
                .Include(p => p.ItemsNecesarios)
                .OrderBy(p => p.Ciudad)
                .ThenBy(p => p.Nombre)
                .ToListAsync();

            return Ok(puntos.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando puntos de acopio");
            return StatusCode(500, new { mensaje = "No se pudieron cargar los puntos de acopio" });
        }
    }

    /// <summary>
    /// Busca el punto de acopio asociado al codigo publico de un voluntario
    /// (ej. "001"). Lo usa el modulo "quiero donar" del frontend.
    /// </summary>
    [HttpGet("buscar")]
    public async Task<IActionResult> BuscarPorCodigo([FromQuery] string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return BadRequest(new { mensaje = "Indica un codigo o nombre a buscar" });

        var buscado = codigo.Trim();
        var termino = buscado.ToLower();

        try
        {
            // Los codigos se guardan con 3 digitos ("001"), pero la gente teclea
            // "1". Se prueban todas las variantes razonables.
            var variantes = new List<string> { buscado };
            if (buscado.All(char.IsDigit) && int.TryParse(buscado, out var numero))
            {
                variantes.Add(numero.ToString("D3"));
                variantes.Add(numero.ToString());
            }
            variantes = variantes.Distinct().ToList();

            var voluntario = await _context.Voluntarios
                .Where(v => v.Activo && v.Codigo != null && variantes.Contains(v.Codigo))
                .FirstOrDefaultAsync();

            // Si no cuadra ningun codigo, se busca por apodo o nombre.
            voluntario ??= await _context.Voluntarios
                .Where(v => v.Activo && (
                    (v.Apodo != null && v.Apodo.ToLower().Contains(termino))
                    || v.Nombre.ToLower().Contains(termino)))
                .FirstOrDefaultAsync();

            PuntoAcopio? punto = null;

            if (voluntario?.PuntoAcopioId != null)
            {
                punto = await _context.PuntosAcopio
                    .Where(p => p.Id == voluntario.PuntoAcopioId.Value && p.Activo)
                    .Include(p => p.Voluntarios)
                    .Include(p => p.ItemsNecesarios)
                    .FirstOrDefaultAsync();
            }

            // Ultimo recurso: por nombre del propio punto o su ciudad.
            punto ??= await _context.PuntosAcopio
                .Where(p => p.Activo && (
                    p.Nombre.ToLower().Contains(termino)
                    || p.Ciudad.ToLower().Contains(termino)))
                .Include(p => p.Voluntarios)
                .Include(p => p.ItemsNecesarios)
                .OrderBy(p => p.Ciudad)
                .ThenBy(p => p.Nombre)
                .FirstOrDefaultAsync();

            if (punto == null)
                return NotFound(new { mensaje = $"No encontramos ningun punto para '{buscado}'" });

            return Ok(MapToDto(punto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error buscando '{Codigo}'", buscado);
            return StatusCode(500, new { mensaje = "No se pudo completar la busqueda" });
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
                return NotFound(new { mensaje = "Punto de acopio no encontrado" });

            return Ok(MapToDto(punto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo el punto {Id}", id);
            return StatusCode(500, new { mensaje = "No se pudo cargar el punto de acopio" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PuntoAcopioDto>> CreatePunto([FromBody] CreatePuntoAcopioDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio" });

        if (string.IsNullOrWhiteSpace(dto.Direccion))
            return BadRequest(new { mensaje = "La direccion es obligatoria" });

        // TimeSpan.Parse lanzaba una excepcion no controlada con formatos raros.
        if (!TimeSpan.TryParse(dto.HorarioInicio, out var horarioInicio))
            return BadRequest(new { mensaje = "Horario de inicio no valido. Usa el formato HH:mm" });

        if (!TimeSpan.TryParse(dto.HorarioFin, out var horarioFin))
            return BadRequest(new { mensaje = "Horario de fin no valido. Usa el formato HH:mm" });

        try
        {
            var punto = new PuntoAcopio
            {
                Nombre = dto.Nombre.Trim(),
                Direccion = dto.Direccion.Trim(),
                Barrio = dto.Barrio?.Trim(),
                Ciudad = string.IsNullOrWhiteSpace(dto.Ciudad) ? "Cali" : dto.Ciudad.Trim(),
                HorarioInicio = horarioInicio,
                HorarioFin = horarioFin,
                Descripcion = dto.Descripcion?.Trim(),
                Telefono = dto.Telefono?.Trim()
            };

            _context.PuntosAcopio.Add(punto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPunto), new { id = punto.Id }, MapToDto(punto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando punto de acopio");
            return StatusCode(500, new { mensaje = "No se pudo crear el punto de acopio" });
        }
    }

    /// <summary>
    /// Edicion parcial: solo se aplican los campos que llegan con valor.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdatePunto(int id, [FromBody] UpdatePuntoAcopioDto dto)
    {
        var punto = await _context.PuntosAcopio.FindAsync(id);
        if (punto == null)
            return NotFound(new { mensaje = "Punto de acopio no encontrado" });

        if (dto.Nombre != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest(new { mensaje = "El nombre no puede quedar vacio" });
            punto.Nombre = dto.Nombre.Trim();
        }

        if (dto.Direccion != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Direccion))
                return BadRequest(new { mensaje = "La direccion no puede quedar vacia" });
            punto.Direccion = dto.Direccion.Trim();
        }

        if (dto.Barrio != null) punto.Barrio = dto.Barrio.Trim();
        if (dto.Ciudad != null && !string.IsNullOrWhiteSpace(dto.Ciudad)) punto.Ciudad = dto.Ciudad.Trim();
        if (dto.Descripcion != null) punto.Descripcion = dto.Descripcion.Trim();
        if (dto.Telefono != null) punto.Telefono = dto.Telefono.Trim();
        if (dto.Activo.HasValue) punto.Activo = dto.Activo.Value;

        if (dto.HorarioInicio != null)
        {
            if (!TimeSpan.TryParse(dto.HorarioInicio, out var inicio))
                return BadRequest(new { mensaje = "Horario de inicio no valido. Usa el formato HH:mm" });
            punto.HorarioInicio = inicio;
        }

        if (dto.HorarioFin != null)
        {
            if (!TimeSpan.TryParse(dto.HorarioFin, out var fin))
                return BadRequest(new { mensaje = "Horario de fin no valido. Usa el formato HH:mm" });
            punto.HorarioFin = fin;
        }

        punto.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando el punto {Id}", id);
            return StatusCode(500, new { mensaje = "No se pudo actualizar el punto de acopio" });
        }

        await _context.Entry(punto).Collection(p => p.Voluntarios).LoadAsync();
        await _context.Entry(punto).Collection(p => p.ItemsNecesarios).LoadAsync();

        return Ok(MapToDto(punto));
    }

    /// <summary>
    /// Sube o reemplaza el flyer del punto de acopio.
    /// Se envia como multipart/form-data con el campo "archivo".
    /// </summary>
    [HttpPost("{id:int}/flyer")]
    [Authorize]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> SubirFlyer(
        int id,
        IFormFile? flyer,
        IFormFile? archivo,
        CancellationToken ct)
    {
        // El frontend envia el campo como "flyer"; se acepta "archivo" como
        // alias para no romper clientes antiguos ni Swagger.
        var imagen = flyer ?? archivo;

        if (!_imagenes.Configurado)
        {
            return StatusCode(503, new
            {
                mensaje = "El almacenamiento de imagenes no esta configurado. Falta la variable Cloudinary__Url."
            });
        }

        if (imagen == null || imagen.Length == 0)
            return BadRequest(new { mensaje = "No se recibio ninguna imagen en el campo 'flyer'" });

        var maxMb = _config.GetValue("FileStorage:MaxFileSizeMB", 5);
        if (imagen.Length > (long)maxMb * 1024 * 1024)
            return BadRequest(new { mensaje = $"La imagen supera el maximo de {maxMb} MB" });

        var tipo = (imagen.ContentType ?? string.Empty).ToLowerInvariant();
        if (!TiposImagenPermitidos.Contains(tipo))
            return BadRequest(new { mensaje = "Formato no permitido. Usa JPG, PNG o WEBP" });

        var punto = await _context.PuntosAcopio.FindAsync(new object?[] { id }, ct);
        if (punto == null)
            return NotFound(new { mensaje = "Punto de acopio no encontrado" });

        try
        {
            // Identificador estable: al resubir se sobreescribe la misma imagen
            // y Cloudinary devuelve una URL con version nueva, asi que el
            // navegador no se queda con la cacheada.
            var publicId = PublicIdFlyer(id);
            var url = await _imagenes.SubirAsync(imagen, publicId, ct);

            punto.UrlFlyer = url;
            punto.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return Ok(new { id = punto.Id, urlFlyer = url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subiendo el flyer del punto {Id}", id);
            return StatusCode(500, new { mensaje = "No se pudo subir el flyer", detalle = ex.Message });
        }
    }

    /// <summary>
    /// Quita el flyer del punto de acopio.
    /// </summary>
    [HttpDelete("{id:int}/flyer")]
    [Authorize]
    public async Task<IActionResult> EliminarFlyer(int id, CancellationToken ct)
    {
        var punto = await _context.PuntosAcopio.FindAsync(new object?[] { id }, ct);
        if (punto == null)
            return NotFound(new { mensaje = "Punto de acopio no encontrado" });

        if (string.IsNullOrWhiteSpace(punto.UrlFlyer))
            return Ok(new { mensaje = "El punto no tenia flyer" });

        if (_imagenes.Configurado)
        {
            try
            {
                await _imagenes.EliminarAsync(PublicIdFlyer(id), ct);
            }
            catch (Exception ex)
            {
                // Si falla el borrado remoto seguimos: lo importante es que deje
                // de mostrarse en la app.
                _logger.LogWarning(ex, "No se pudo borrar el flyer remoto del punto {Id}", id);
            }
        }

        punto.UrlFlyer = null;
        punto.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { mensaje = "Flyer eliminado" });
    }

    // ───────────────────────────────────────────────────────
    private static string PublicIdFlyer(int id) => $"unidosos/puntos/punto-{id}";

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
