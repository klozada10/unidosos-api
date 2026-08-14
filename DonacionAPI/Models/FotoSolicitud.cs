using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("fotos_solicitud")]
public class FotoSolicitud
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("solicitud_id")]
    public int SolicitudId { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("url_foto")]
    public string UrlFoto { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("nombre_archivo")]
    public string? NombreArchivo { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public SolicitudAyuda Solicitud { get; set; } = null!;
}

