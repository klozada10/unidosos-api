using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonacionAPI.Models;

[Table("puntos_acopio")]
public class PuntoAcopio
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

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

    [Column("horario_inicio")]
    public TimeOnly HorarioInicio { get; set; } = new TimeOnly(8, 0);

    [Column("horario_fin")]
    public TimeOnly HorarioFin { get; set; } = new TimeOnly(18, 0);

    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("latitud")]
    public decimal? Latitud { get; set; }

    [Column("longitud")]
    public decimal? Longitud { get; set; }

    [MaxLength(20)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public ICollection<Voluntario> Voluntarios { get; set; } = new List<Voluntario>();
    public ICollection<ItemNecesario> ItemsNecesarios { get; set; } = new List<ItemNecesario>();
    public ICollection<InventarioDonacion> Inventario { get; set; } = new List<InventarioDonacion>();
}
