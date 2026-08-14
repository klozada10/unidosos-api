using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using DonacionAPI.Data;

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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var jwtKey = builder.Configuration["Auth:JwtKey"]
    ?? throw new InvalidOperationException("Auth:JwtKey no configurada");

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
