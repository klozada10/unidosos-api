namespace DonacionAPI.DTOs;

public class PuntoAcopioDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Barrio { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string HorarioInicio { get; set; } = string.Empty;
    public string HorarioFin { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Telefono { get; set; }
    public string? UrlFlyer { get; set; }   // Imagen del flyer
    public List<VoluntarioDto> Voluntarios { get; set; } = new();
    public List<ItemNecesarioDto> ItemsNecesarios { get; set; } = new();
}

public class VoluntarioDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Apodo { get; set; }
    public string? Telefono { get; set; }
    public string? Codigo { get; set; }     // Código público del influencer (ej: "001")
}

public class ItemNecesarioDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Prioridad { get; set; } = "media";
}

public class CreatePuntoAcopioDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Barrio { get; set; }
    public string Ciudad { get; set; } = "Cali";
    public string HorarioInicio { get; set; } = "08:00";
    public string HorarioFin { get; set; } = "18:00";
    public string? Descripcion { get; set; }
    public string? Telefono { get; set; }
}
