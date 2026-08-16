using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using DonacionAPI.Data;
using DonacionAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "UNIDOSOS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Token. Ejemplo: Bearer {tu-token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
        Array.Empty<string>()
    }});
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// ServerVersion.AutoDetect abre una conexion a MySQL. Dentro del lambda de
// AddDbContext se ejecuta en CADA request (una conexion extra por peticion, y
// cualquier fallo de credenciales revienta el request entero). Se resuelve una
// sola vez al arrancar, con fallback para que la app levante aunque la BD falle.
ServerVersion serverVersion;
try
{
    serverVersion = ServerVersion.AutoDetect(connectionString);
    Console.WriteLine($"[startup] MySQL detectado: {serverVersion}");
}
catch (Exception exVersion)
{
    Console.WriteLine($"[startup] No se pudo autodetectar MySQL ({exVersion.GetType().Name}: {exVersion.Message}). Usando 8.0.36 por defecto.");
    serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Almacenamiento de imagenes (flyers). Si no hay credenciales de Cloudinary
// la app arranca igual y los endpoints de subida responden 503.
builder.Services.AddHttpClient();
builder.Services.AddScoped<IImagenService, CloudinaryService>();

// Ojo: "??" solo salta con null. Una cadena vacia lo atravesaba y despues
// reventaba dentro de SymmetricSecurityKey con "key length is zero", que es
// un 500 imposible de diagnosticar. Se valida tambien el vacio y la longitud
// minima que exige HMAC-SHA256 (32 bytes).
var jwtKey = builder.Configuration["Auth:JwtKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Auth:JwtKey no configurada. Definela como variable de entorno Auth__JwtKey.");
}
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Auth:JwtKey es demasiado corta: HMAC-SHA256 exige al menos 32 caracteres.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "UNIDOSOS",
            ValidAudience = "UNIDOSOS-App",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

// Handler de errores. Con ?diag=1 devuelve el detalle real de la excepcion
// (temporal, para depurar el 500 de /api/puntosacopio).
app.UseExceptionHandler(h => h.Run(async ctx =>
{
    var feature = ctx.Features.Get<IExceptionHandlerPathFeature>();
    var ex = feature?.Error;

    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";

    if (ctx.Request.Query.ContainsKey("diag"))
    {
        await ctx.Response.WriteAsJsonAsync(new
        {
            mensaje = "Error interno",
            ruta = feature?.Path,
            tipo = ex?.GetType().FullName,
            detalle = ex?.Message,
            innerTipo = ex?.InnerException?.GetType().FullName,
            innerDetalle = ex?.InnerException?.Message,
            stack = ex?.StackTrace?.Split('\n').Take(10).Select(s => s.Trim()).ToArray()
        });
        return;
    }

    await ctx.Response.WriteAsJsonAsync(new { mensaje = "Error interno" });
}));

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Marcador de build para confirmar que Railway desplego esta version
app.MapGet("/api/diag/version", () => Results.Ok(new
{
    build = "diag-1",
    utc = DateTime.UtcNow
}));

// Prueba de conexion cruda a MySQL, sin EF: aisla si el problema es la BD,
// el esquema o el mapeo de EF.
app.MapGet("/api/diag/db", async (AppDbContext db) =>
{
    var resultado = new Dictionary<string, object?>();

    // Cadena de conexion enmascarada: confirma que Railway inyecta la variable
    // correcta, sin exponer la contrasena.
    try
    {
        var cs = db.Database.GetDbConnection().ConnectionString ?? string.Empty;
        var partes = cs.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(par =>
            {
                var i = par.IndexOf('=');
                if (i < 0) return par.Trim();
                var clave = par[..i].Trim();
                var valor = par[(i + 1)..].Trim();
                if (clave.Contains("password", StringComparison.OrdinalIgnoreCase) || clave.Equals("pwd", StringComparison.OrdinalIgnoreCase))
                    return clave + "=<" + valor.Length + " caracteres>";
                return clave + "=" + valor;
            })
            .ToArray();
        resultado["cadenaConexion"] = partes;
    }
    catch (Exception ex)
    {
        resultado["cadenaConexion"] = $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }

    try
    {
        resultado["puedeConectar"] = await db.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        resultado["puedeConectar"] = $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }

    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SHOW COLUMNS FROM puntos_acopio";
        using var reader = await cmd.ExecuteReaderAsync();
        var cols = new List<object>();
        while (await reader.ReadAsync())
        {
            cols.Add(new
            {
                campo = reader["Field"]?.ToString(),
                tipo = reader["Type"]?.ToString(),
                nulo = reader["Null"]?.ToString()
            });
        }
        resultado["columnasPuntosAcopio"] = cols;
    }
    catch (Exception ex)
    {
        resultado["columnasPuntosAcopio"] = $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }

    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SHOW COLUMNS FROM voluntarios";
        using var reader = await cmd.ExecuteReaderAsync();
        var cols = new List<object>();
        while (await reader.ReadAsync())
        {
            cols.Add(new { campo = reader["Field"]?.ToString(), tipo = reader["Type"]?.ToString() });
        }
        resultado["columnasVoluntarios"] = cols;
    }
    catch (Exception ex)
    {
        resultado["columnasVoluntarios"] = $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }

    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SHOW COLUMNS FROM items_necesarios";
        using var reader = await cmd.ExecuteReaderAsync();
        var cols = new List<object>();
        while (await reader.ReadAsync())
        {
            cols.Add(new { campo = reader["Field"]?.ToString(), tipo = reader["Type"]?.ToString() });
        }
        resultado["columnasItemsNecesarios"] = cols;
    }
    catch (Exception ex)
    {
        resultado["columnasItemsNecesarios"] = $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }

    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) AS total, SUM(horario_inicio IS NULL) AS inicioNulo, SUM(horario_fin IS NULL) AS finNulo FROM puntos_acopio";
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            resultado["filas"] = new
            {
                total = reader["total"]?.ToString(),
                inicioNulo = reader["inicioNulo"]?.ToString(),
                finNulo = reader["finNulo"]?.ToString()
            };
        }
    }
    catch (Exception ex)
    {
        resultado["filas"] = $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }

    return Results.Ok(resultado);
});

var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);

app.Run();
