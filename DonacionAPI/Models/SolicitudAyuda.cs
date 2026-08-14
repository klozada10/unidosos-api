using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("solicitudes_ayuda")]
public class SolicitudAyuda
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("apellido")]
    public string Apellido { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("celular")]
    public string Celular { get; set; } = string.Empty;

    [MaxLength(150)]
    [Column("correo")]
    public string? Correo { get; set; }

    [Required]
    [MaxLength(300)]
    [Column("direccion")]
    public string Direccion { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("barrio")]
    public string? Barrio { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("ciudad")]
    public string Ciudad { get; set; } = "Cali";

    [Required]
    [Column("descripcion_necesidad")]
    public string DescripcionNecesidad { get; set; } = string.Empty;

    [Column("estado")]
    public string Estado { get; set; } = "pendiente"; // pendiente, en_proceso, atendida

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public ICollection<FotoSolicitud> Fotos { get; set; } = new List<FotoSolicitud>();
}
 
