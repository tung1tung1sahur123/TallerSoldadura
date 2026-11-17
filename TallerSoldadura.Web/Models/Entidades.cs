namespace TallerSoldadura.Web.Models;

// Cada clase corresponde a una tabla de la base de datos TallerSoldadura (Actividad 9.1).

public class Rol
{
    public int IdRol { get; set; }
    public string NombreRol { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public List<UsuarioRol> Usuarios { get; set; } = new();
}

public class Usuario
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string? Matricula { get; set; }
    public string CorreoInstitucional { get; set; } = "";
    public string HashContrasena { get; set; } = "";
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.Now;
    public DateTime FechaModificacion { get; set; } = DateTime.Now;
    public List<UsuarioRol> Roles { get; set; } = new();

    public string NombreCompleto => $"{Nombre} {Apellidos}";
}

public class UsuarioRol
{
    public int IdUsuario { get; set; }
    public int IdRol { get; set; }
    public DateTime FechaAsignacion { get; set; } = DateTime.Now;
    public Usuario Usuario { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
}

public class HorarioApertura
{
    public int IdHorario { get; set; }
    public short DiaSemana { get; set; }          // 1 = lunes ... 7 = domingo
    public TimeOnly HoraApertura { get; set; }
    public TimeOnly HoraCierre { get; set; }
    public bool Activo { get; set; } = true;

    public static readonly string[] Dias = { "", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
    public string NombreDia => DiaSemana is >= 1 and <= 7 ? Dias[DiaSemana] : "?";
}

public static class Estados
{
    public const string Pendiente = "pendiente";
    public const string Autorizada = "autorizada";
    public const string Rechazada = "rechazada";
    public const string Cancelada = "cancelada";
}

public class Reservacion
{
    public int IdReservacion { get; set; }
    public int IdAlumno { get; set; }
    public DateOnly FechaUso { get; set; }
    public TimeOnly HoraEntrada { get; set; }
    public TimeOnly HoraSalida { get; set; }
    public string Estado { get; set; } = Estados.Pendiente;
    public DateTime FechaSolicitud { get; set; } = DateTime.Now;
    public int? IdMaestroRevision { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? ComentarioRevision { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public DateTime FechaModificacion { get; set; } = DateTime.Now;

    public Usuario Alumno { get; set; } = null!;
    public Usuario? MaestroRevision { get; set; }
    public Asistencia? Asistencia { get; set; }
}

public class Asistencia
{
    public int IdReservacion { get; set; }
    public DateTime EntradaReal { get; set; }
    public DateTime? SalidaReal { get; set; }
    public int IdMaestroEntrada { get; set; }
    public int? IdMaestroSalida { get; set; }
    public Reservacion Reservacion { get; set; } = null!;

    public double? DuracionMinutos => SalidaReal is null ? null : Math.Round((SalidaReal.Value - EntradaReal).TotalMinutes, 2);
}

public class Notificacion
{
    public int IdNotificacion { get; set; }
    public int IdReservacion { get; set; }
    public int IdDestinatario { get; set; }
    public string EstadoNotificado { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaLectura { get; set; }
}

public class BitacoraMovimiento
{
    public int IdMovimiento { get; set; }
    public int IdReservacion { get; set; }
    public int? IdActor { get; set; }              // null = acción automática del sistema
    public string Accion { get; set; } = "";
    public DateTime FechaEvento { get; set; } = DateTime.Now;
    public string? ValorAnterior { get; set; }
    public string ValorNuevo { get; set; } = "";
    public Usuario? Actor { get; set; }
}
