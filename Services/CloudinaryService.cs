using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DonacionAPI.Services;

/// <summary>
/// Sube imagenes a Cloudinary usando su API REST directamente, sin SDK.
///
/// Configuracion (cualquiera de las dos formas):
///   Cloudinary:Url        -> cloudinary://API_KEY:API_SECRET@CLOUD_NAME
///   Cloudinary:CloudName + Cloudinary:ApiKey + Cloudinary:ApiSecret
///
/// En Railway se definen como variables de entorno:
///   Cloudinary__Url   (doble guion bajo)
/// </summary>
public class CloudinaryService : IImagenService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CloudinaryService> _logger;

    private readonly string _cloudName;
    private readonly string _apiKey;
    private readonly string _apiSecret;

    public CloudinaryService(
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<CloudinaryService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;

        var cloudName = config["Cloudinary:CloudName"];
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];

        // Formato compacto cloudinary://key:secret@cloud
        var url = config["Cloudinary:Url"];
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (TryParseCloudinaryUrl(url, out var c, out var k, out var s))
            {
                cloudName = c;
                apiKey = k;
                apiSecret = s;
            }
            else
            {
                _logger.LogWarning(
                    "Cloudinary:Url no tiene el formato cloudinary://API_KEY:API_SECRET@CLOUD_NAME. Se ignora.");
            }
        }

        _cloudName = cloudName ?? string.Empty;
        _apiKey = apiKey ?? string.Empty;
        _apiSecret = apiSecret ?? string.Empty;

        if (!Configurado)
        {
            _logger.LogWarning(
                "Cloudinary no esta configurado. La subida de imagenes respondera 503 hasta que se definan las credenciales.");
        }
    }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(_cloudName)
        && !string.IsNullOrWhiteSpace(_apiKey)
        && !string.IsNullOrWhiteSpace(_apiSecret);

    public async Task<string> SubirAsync(IFormFile archivo, string publicId, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new InvalidOperationException("Cloudinary no esta configurado.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        // Parametros que entran en la firma: orden alfabetico, sin el archivo
        // ni api_key. Cualquier desajuste aqui produce un 401 de Cloudinary.
        var aFirmar = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["invalidate"] = "true",
            ["overwrite"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = timestamp
        };

        var firma = Firmar(aFirmar);

        using var contenido = new MultipartFormDataContent();

        var stream = archivo.OpenReadStream();
        var archivoContent = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(archivo.ContentType))
        {
            archivoContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(archivo.ContentType);
        }
        contenido.Add(archivoContent, "file", archivo.FileName);

        foreach (var par in aFirmar)
            contenido.Add(new StringContent(par.Value), par.Key);

        contenido.Add(new StringContent(_apiKey), "api_key");
        contenido.Add(new StringContent(firma), "signature");

        var cliente = _httpFactory.CreateClient(nameof(CloudinaryService));
        cliente.Timeout = TimeSpan.FromSeconds(60);

        var endpoint = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload";
        using var respuesta = await cliente.PostAsync(endpoint, contenido, ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogError("Cloudinary devolvio {Codigo}: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
            throw new InvalidOperationException(
                $"Cloudinary rechazo la subida ({(int)respuesta.StatusCode}): {ExtraerError(cuerpo)}");
        }

        using var doc = JsonDocument.Parse(cuerpo);
        if (doc.RootElement.TryGetProperty("secure_url", out var secureUrl))
        {
            var valor = secureUrl.GetString();
            if (!string.IsNullOrWhiteSpace(valor))
                return valor;
        }

        throw new InvalidOperationException("Cloudinary no devolvio secure_url.");
    }

    public async Task EliminarAsync(string publicId, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new InvalidOperationException("Cloudinary no esta configurado.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var aFirmar = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["invalidate"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = timestamp
        };

        var firma = Firmar(aFirmar);

        using var contenido = new MultipartFormDataContent();
        foreach (var par in aFirmar)
            contenido.Add(new StringContent(par.Value), par.Key);
        contenido.Add(new StringContent(_apiKey), "api_key");
        contenido.Add(new StringContent(firma), "signature");

        var cliente = _httpFactory.CreateClient(nameof(CloudinaryService));
        cliente.Timeout = TimeSpan.FromSeconds(30);

        var endpoint = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/destroy";
        using var respuesta = await cliente.PostAsync(endpoint, contenido, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            // No relanzamos: si la imagen ya no esta, el borrado logico en BD
            // debe completarse igualmente.
            _logger.LogWarning(
                "No se pudo borrar {PublicId} en Cloudinary ({Codigo}): {Cuerpo}",
                publicId, (int)respuesta.StatusCode, cuerpo);
        }
    }

    // ───────────────────────────────────────────────────────
    private string Firmar(SortedDictionary<string, string> parametros)
    {
        var sb = new StringBuilder();
        foreach (var par in parametros)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(par.Key).Append('=').Append(par.Value);
        }
        sb.Append(_apiSecret);

        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ExtraerError(string cuerpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var mensaje))
            {
                return mensaje.GetString() ?? cuerpo;
            }
        }
        catch (JsonException)
        {
            // cuerpo no era JSON
        }
        return cuerpo;
    }

    private static bool TryParseCloudinaryUrl(
        string url, out string cloudName, out string apiKey, out string apiSecret)
    {
        cloudName = apiKey = apiSecret = string.Empty;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, "cloudinary", StringComparison.OrdinalIgnoreCase))
            return false;

        var credenciales = uri.UserInfo.Split(':', 2);
        if (credenciales.Length != 2)
            return false;

        cloudName = uri.Host;
        apiKey = Uri.UnescapeDataString(credenciales[0]);
        apiSecret = Uri.UnescapeDataString(credenciales[1]);

        return !string.IsNullOrWhiteSpace(cloudName)
            && !string.IsNullOrWhiteSpace(apiKey)
            && !string.IsNullOrWhiteSpace(apiSecret);
    }
}
