using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("items_necesarios")]
public class ItemNecesario
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("punto_acopio_id")]
    public int PuntoAcopioId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [Column("prioridad")]
    public string Prioridad { get; set; } = "media"; // alta, media, baja

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public PuntoAcopio PuntoAcopio { get; set; } = null!;
}
 
