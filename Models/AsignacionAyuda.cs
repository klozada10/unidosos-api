using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("asignaciones_ayuda")]
public class AsignacionAyuda
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("solicitud_id")]
    public int SolicitudId { get; set; }

    [Column("voluntario_id")]
    public int VoluntarioId { get; set; }

    /// <summary>asignado = "yo quiero ayudar al #15" | entregado = check marcado ✓</summary>
    [Column("estado")]
    public string Estado { get; set; } = "asignado"; // asignado | entregado

    [Column("fecha_asignacion")]
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

    [Column("fecha_entrega")]
    public DateTime? FechaEntrega { get; set; }

    [MaxLength(400)]
    [Column("notas")]
    public string? Notas { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public SolicitudAyuda Solicitud { get; set; } = null!;
    public Voluntario Voluntario { get; set; } = null!;
}
