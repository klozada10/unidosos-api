using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("voluntarios")]
public class Voluntario
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("punto_acopio_id")]
    public int PuntoAcopioId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("apellido")]
    public string Apellido { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("apodo")]
    public string? Apodo { get; set; }

    [MaxLength(20)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public PuntoAcopio PuntoAcopio { get; set; } = null!;
}
