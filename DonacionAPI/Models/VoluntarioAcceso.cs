using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("voluntarios_acceso")]
public class VoluntarioAcceso
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("voluntario_id")]
    public int VoluntarioId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("es_super_admin")]
    public bool EsSuperAdmin { get; set; } = false;

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("ultimo_acceso")]
    public DateTime? UltimoAcceso { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public Voluntario Voluntario { get; set; } = null!;
}
 
