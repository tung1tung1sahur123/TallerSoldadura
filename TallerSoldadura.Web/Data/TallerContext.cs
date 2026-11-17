using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Models;

namespace TallerSoldadura.Web.Data;

// Mapea las clases a las tablas ya creadas con el script TallerSoldadura.sql (no usa migraciones).
public class TallerContext(DbContextOptions<TallerContext> options) : DbContext(options)
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<HorarioApertura> Horarios => Set<HorarioApertura>();
    public DbSet<Reservacion> Reservaciones => Set<Reservacion>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<BitacoraMovimiento> Bitacora => Set<BitacoraMovimiento>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Rol>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.IdRol);
            e.Property(x => x.IdRol).HasColumnName("id_rol");
            e.Property(x => x.NombreRol).HasColumnName("nombre_rol");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
        });

        mb.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.IdUsuario);
            e.Property(x => x.IdUsuario).HasColumnName("id_usuario");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Apellidos).HasColumnName("apellidos");
            e.Property(x => x.Matricula).HasColumnName("matricula");
            e.Property(x => x.CorreoInstitucional).HasColumnName("correo_institucional");
            e.Property(x => x.HashContrasena).HasColumnName("hash_contrasena");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.FechaAlta).HasColumnName("fecha_alta");
            e.Property(x => x.FechaModificacion).HasColumnName("fecha_modificacion");
            e.Ignore(x => x.NombreCompleto);
        });

        mb.Entity<UsuarioRol>(e =>
        {
            e.ToTable("usuario_rol");
            e.HasKey(x => new { x.IdUsuario, x.IdRol });
            e.Property(x => x.IdUsuario).HasColumnName("id_usuario");
            e.Property(x => x.IdRol).HasColumnName("id_rol");
            e.Property(x => x.FechaAsignacion).HasColumnName("fecha_asignacion");
            e.HasOne(x => x.Usuario).WithMany(u => u.Roles).HasForeignKey(x => x.IdUsuario);
            e.HasOne(x => x.Rol).WithMany(r => r.Usuarios).HasForeignKey(x => x.IdRol);
        });

        mb.Entity<HorarioApertura>(e =>
        {
            e.ToTable("horarios_apertura");
            e.HasKey(x => x.IdHorario);
            e.Property(x => x.IdHorario).HasColumnName("id_horario");
            e.Property(x => x.DiaSemana).HasColumnName("dia_semana");
            e.Property(x => x.HoraApertura).HasColumnName("hora_apertura");
            e.Property(x => x.HoraCierre).HasColumnName("hora_cierre");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Ignore(x => x.NombreDia);
        });

        mb.Entity<Reservacion>(e =>
        {
            e.ToTable("reservaciones");
            e.HasKey(x => x.IdReservacion);
            e.Property(x => x.IdReservacion).HasColumnName("id_reservacion");
            e.Property(x => x.IdAlumno).HasColumnName("id_alumno");
            e.Property(x => x.FechaUso).HasColumnName("fecha_uso");
            e.Property(x => x.HoraEntrada).HasColumnName("hora_entrada");
            e.Property(x => x.HoraSalida).HasColumnName("hora_salida");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.FechaSolicitud).HasColumnName("fecha_solicitud");
            e.Property(x => x.IdMaestroRevision).HasColumnName("id_maestro_revision");
            e.Property(x => x.FechaRevision).HasColumnName("fecha_revision");
            e.Property(x => x.ComentarioRevision).HasColumnName("comentario_revision");
            e.Property(x => x.FechaCancelacion).HasColumnName("fecha_cancelacion");
            e.Property(x => x.FechaModificacion).HasColumnName("fecha_modificacion");
            e.HasOne(x => x.Alumno).WithMany().HasForeignKey(x => x.IdAlumno).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.MaestroRevision).WithMany().HasForeignKey(x => x.IdMaestroRevision).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Asistencia).WithOne(a => a.Reservacion).HasForeignKey<Asistencia>(a => a.IdReservacion);
        });

        mb.Entity<Asistencia>(e =>
        {
            e.ToTable("asistencias");
            e.HasKey(x => x.IdReservacion);
            e.Property(x => x.IdReservacion).HasColumnName("id_reservacion").ValueGeneratedNever();
            e.Property(x => x.EntradaReal).HasColumnName("entrada_real");
            e.Property(x => x.SalidaReal).HasColumnName("salida_real");
            e.Property(x => x.IdMaestroEntrada).HasColumnName("id_maestro_entrada");
            e.Property(x => x.IdMaestroSalida).HasColumnName("id_maestro_salida");
            e.Ignore(x => x.DuracionMinutos);
        });

        mb.Entity<Notificacion>(e =>
        {
            e.ToTable("notificaciones");
            e.HasKey(x => x.IdNotificacion);
            e.Property(x => x.IdNotificacion).HasColumnName("id_notificacion");
            e.Property(x => x.IdReservacion).HasColumnName("id_reservacion");
            e.Property(x => x.IdDestinatario).HasColumnName("id_destinatario");
            e.Property(x => x.EstadoNotificado).HasColumnName("estado_notificado");
            e.Property(x => x.Mensaje).HasColumnName("mensaje");
            e.Property(x => x.FechaCreacion).HasColumnName("fecha_creacion");
            e.Property(x => x.FechaLectura).HasColumnName("fecha_lectura");
        });

        mb.Entity<BitacoraMovimiento>(e =>
        {
            e.ToTable("bitacora_movimientos");
            e.HasKey(x => x.IdMovimiento);
            e.Property(x => x.IdMovimiento).HasColumnName("id_movimiento");
            e.Property(x => x.IdReservacion).HasColumnName("id_reservacion");
            e.Property(x => x.IdActor).HasColumnName("id_actor");
            e.Property(x => x.Accion).HasColumnName("accion");
            e.Property(x => x.FechaEvento).HasColumnName("fecha_evento");
            e.Property(x => x.ValorAnterior).HasColumnName("valor_anterior");
            e.Property(x => x.ValorNuevo).HasColumnName("valor_nuevo");
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.IdActor).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
