using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("movimientos_donacion")]
public class MovimientoDonacion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("inventario_id")]
    public int InventarioId { get; set; }

    [Column("cantidad")]
    public decimal Cantidad { get; set; }

    [MaxLength(150)]
    [Column("nombre_donante")]
    public string? NombreDonante { get; set; }

    [MaxLength(300)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    [Column("fecha_recepcion")]
    public DateTime FechaRecepcion { get; set; } = DateTime.UtcNow;

    // Navegación
    public InventarioDonacion Inventario { get; set; } = null!;
}
 
