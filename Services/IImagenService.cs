namespace DonacionAPI.Services;

/// <summary>
/// Almacenamiento de imagenes (flyers de puntos de acopio, fotos de solicitudes).
/// La implementacion actual sube a Cloudinary.
/// </summary>
public interface IImagenService
{
    /// <summary>
    /// Indica si hay credenciales configuradas. Si es false, los endpoints de
    /// subida deben responder 503 en vez de reventar con un 500 opaco.
    /// </summary>
    bool Configurado { get; }

    /// <summary>
    /// Sube (o reemplaza) una imagen con un identificador estable.
    /// Devuelve la URL publica definitiva.
    /// </summary>
    Task<string> SubirAsync(IFormFile archivo, string publicId, CancellationToken ct = default);

    /// <summary>
    /// Borra una imagen previamente subida con ese identificador.
    /// </summary>
    Task EliminarAsync(string publicId, CancellationToken ct = default);
}
