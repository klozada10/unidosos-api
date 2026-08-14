using Microsoft.EntityFrameworkCore;
using DonacionAPI.Models;

namespace DonacionAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PuntoAcopio> PuntosAcopio { get; set; }
    public DbSet<Voluntario> Voluntarios { get; set; }
    public DbSet<ItemNecesario> ItemsNecesarios { get; set; }
    public DbSet<InventarioDonacion> InventarioDonaciones { get; set; }
    public DbSet<MovimientoDonacion> MovimientosDonacion { get; set; }
    public DbSet<SolicitudAyuda> SolicitudesAyuda { get; set; }
    public DbSet<FotoSolicitud> FotosSolicitud { get; set; }
    public DbSet<VoluntarioAcceso> VoluntariosAcceso { get; set; }
    public DbSet<AsignacionAyuda> AsignacionesAyuda { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Índice único para evitar duplicar items en inventario
        modelBuilder.Entity<InventarioDonacion>()
            .HasIndex(i => new { i.PuntoAcopioId, i.NombreItem })
            .IsUnique();

        // Relaciones
        modelBuilder.Entity<Voluntario>()
            .HasOne(v => v.PuntoAcopio)
            .WithMany(p => p.Voluntarios)
            .HasForeignKey(v => v.PuntoAcopioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ItemNecesario>()
            .HasOne(i => i.PuntoAcopio)
            .WithMany(p => p.ItemsNecesarios)
            .HasForeignKey(i => i.PuntoAcopioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventarioDonacion>()
            .HasOne(i => i.PuntoAcopio)
            .WithMany(p => p.Inventario)
            .HasForeignKey(i => i.PuntoAcopioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MovimientoDonacion>()
            .HasOne(m => m.Inventario)
            .WithMany(i => i.Movimientos)
            .HasForeignKey(m => m.InventarioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FotoSolicitud>()
            .HasOne(f => f.Solicitud)
            .WithMany(s => s.Fotos)
            .HasForeignKey(f => f.SolicitudId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VoluntarioAcceso>()
            .HasOne(va => va.Voluntario)
            .WithMany()
            .HasForeignKey(va => va.VoluntarioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AsignacionAyuda>()
            .HasOne(a => a.Solicitud)
            .WithMany()
            .HasForeignKey(a => a.SolicitudId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AsignacionAyuda>()
            .HasOne(a => a.Voluntario)
            .WithMany()
            .HasForeignKey(a => a.VoluntarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
