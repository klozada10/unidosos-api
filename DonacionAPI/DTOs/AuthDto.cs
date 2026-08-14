namespace DonacionAPI.DTOs;

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Apodo { get; set; }
    public bool EsSuperAdmin { get; set; }
    public int VoluntarioId { get; set; }
    public int PuntoAcopioId { get; set; }
    public string NombrePunto { get; set; } = string.Empty;
    public DateTime Expiracion { get; set; }
}

public class SetupPasswordDto
{
    public string Username { get; set; } = string.Empty;
    public string NuevaPassword { get; set; } = string.Empty;
    public string CodigoAdmin { get; set; } = string.Empty;
}

public class RegistroVoluntarioDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Apodo { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public int? PuntoAcopioId { get; set; }  // Opcional — el voluntario puede crear su propio punto después
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
