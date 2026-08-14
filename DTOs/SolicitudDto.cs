namespace DonacionAPI.DTOs;

public class SolicitudAyudaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? Barrio { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string DescripcionNecesidad { get; set; } = string.Empty;
    public string Estado { get; set; } = "pendiente";
    public List<string> UrlFotos { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CreateSolicitudDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? Barrio { get; set; }
    public string Ciudad { get; set; } = "Cali";
    public string DescripcionNecesidad { get; set; } = string.Empty;
}
