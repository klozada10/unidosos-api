namespace DonacionAPI.DTOs;

/// <summary>
/// Vista de una solicitud de ayuda para el panel de voluntarios
/// Incluye número incremental, cuántas veces ha sido atendida, y quiénes la atendieron
/// </summary>
public class SolicitudAdminDto
{
    public int Id { get; set; }                  // Número incremental público: #1, #2, #15...
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? Barrio { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string DescripcionNecesidad { get; set; } = string.Empty;
    public string Estado { get; set; } = "pendiente";
    public List<string> UrlFotos { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // Info de asignaciones
    public int TotalVecesAtendida { get; set; }       // Cuántas veces ha recibido ayuda (entregado ✓)
    public int TotalAsignados { get; set; }            // Cuántos voluntarios dijeron "yo ayudo"
    public List<AsignacionResumenDto> Asignaciones { get; set; } = new();
    public bool YoLaTengoAsignada { get; set; }       // El voluntario actual ya la tiene asignada
    public bool YoLaEntregue { get; set; }             // El voluntario actual ya la marcó entregada
}

public class AsignacionResumenDto
{
    public int Id { get; set; }
    public string NombreVoluntario { get; set; } = string.Empty;
    public string? ApodoVoluntario { get; set; }
    public string Estado { get; set; } = string.Empty;    // asignado | entregado
    public DateTime FechaAsignacion { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public string? Notas { get; set; }
}

/// <summary>Asignar al voluntario actual para ayudar a una persona</summary>
public class AsignarAyudaDto
{
    public int SolicitudId { get; set; }
    public string? Notas { get; set; }
}

/// <summary>Marcar como entregado (check ✓)</summary>
public class MarcarEntregadoDto
{
    public string? Notas { get; set; }
}
