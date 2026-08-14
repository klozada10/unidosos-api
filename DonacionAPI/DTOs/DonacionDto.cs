namespace DonacionAPI.DTOs;

public class InventarioItemDto
{
    public int Id { get; set; }
    public string NombreItem { get; set; } = string.Empty;
    public string Unidad { get; set; } = "unidad";
    public decimal CantidadTotal { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO para agregar o actualizar un ítem del inventario.
/// Si el ítem ya existe, se suma la cantidad. Si no, se crea.
/// </summary>
public class AgregarDonacionDto
{
    public int PuntoAcopioId { get; set; }
    public string NombreItem { get; set; } = string.Empty;
    public string Unidad { get; set; } = "unidad";
    public decimal Cantidad { get; set; }
    public string? NombreDonante { get; set; }
    public string? Observacion { get; set; }
}
