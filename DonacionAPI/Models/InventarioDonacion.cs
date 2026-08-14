using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("inventario_donaciones")]
public class InventarioDonacion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("punto_acopio_id")]
    public int PuntoAcopioId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("nombre_item")]
    public string NombreItem { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("unidad")]
    public string Unidad { get; set; } = "unidad";

    [Column("cantidad_total")]
    public decimal CantidadTotal { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public PuntoAcopio PuntoAcopio { get; set; } = null!;
    public ICollection<MovimientoDonacion> Movimientos { get; set; } = new List<MovimientoDonacion>();
}
