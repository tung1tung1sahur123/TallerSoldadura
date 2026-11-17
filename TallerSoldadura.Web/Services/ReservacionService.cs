using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;

namespace TallerSoldadura.Web.Services;

/// <summary>
/// Reglas del proceso de reservación (diccionario de datos R02–R09):
/// validación de horario, empalmes, cambios de estado, bitácora y avisos.
/// </summary>
public class ReservacionService(TallerContext db)
{
    public record Resultado(bool Ok, string Mensaje, int? Folio = null);

    // R02 y R03: intervalo válido y dentro de una franja activa del día.
    public async Task<string?> ValidarHorarioAsync(DateOnly fecha, TimeOnly entrada, TimeOnly salida, bool exigirFuturo = true)
    {
        if (salida <= entrada) return "La hora de salida debe ser mayor que la hora de entrada.";
        if (exigirFuturo && fecha.ToDateTime(entrada) <= DateTime.Now) return "El horario solicitado ya pasó.";

        short dia = (short)(fecha.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)fecha.DayOfWeek);
        bool cabe = await db.Horarios.AnyAsync(h => h.Activo && h.DiaSemana == dia
                                                     && h.HoraApertura <= entrada && salida <= h.HoraCierre);
        return cabe ? null : "El taller no está abierto en ese horario. Revisa los horarios de apertura.";
    }

    // R04: empalme contra reservaciones autorizadas del mismo día (intervalos contiguos sí se permiten).
    public Task<bool> HayEmpalmeAsync(DateOnly fecha, TimeOnly entrada, TimeOnly salida, int? excluirId = null) =>
        db.Reservaciones.AnyAsync(r => r.Estado == Estados.Autorizada && r.FechaUso == fecha
                                       && r.IdReservacion != (excluirId ?? 0)
                                       && entrada < r.HoraSalida && salida > r.HoraEntrada);

    public async Task<Resultado> SolicitarAsync(int idAlumno, DateOnly fecha, TimeOnly entrada, TimeOnly salida)
    {
        var error = await ValidarHorarioAsync(fecha, entrada, salida);
        if (error is not null) return new(false, error);
        if (await HayEmpalmeAsync(fecha, entrada, salida))
            return new(false, "Ese horario ya está ocupado por otra reservación autorizada.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = new Reservacion { IdAlumno = idAlumno, FechaUso = fecha, HoraEntrada = entrada, HoraSalida = salida };
        db.Reservaciones.Add(r);
        await db.SaveChangesAsync();
        Registrar(r.IdReservacion, idAlumno, "crear", null, new { estado = r.Estado, fecha_uso = fecha, entrada, salida });
        Avisar(r, $"Tu solicitud #{r.IdReservacion} para el {fecha:dd/MM/yyyy} de {entrada:HH\\:mm} a {salida:HH\\:mm} quedó pendiente de revisión.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(true, $"Solicitud registrada con folio {r.IdReservacion}.", r.IdReservacion);
    }

    // R05 y R07: la segunda validación y la autorización se ejecutan en una transacción serializable.
    public async Task<Resultado> RevisarAsync(int idReservacion, int idMaestro, bool autorizar, string? comentario)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var r = await db.Reservaciones.FirstOrDefaultAsync(x => x.IdReservacion == idReservacion);
        if (r is null) return new(false, "La solicitud no existe.");
        if (r.Estado != Estados.Pendiente) return new(false, "La solicitud ya fue revisada.");

        if (autorizar)
        {
            var error = await ValidarHorarioAsync(r.FechaUso, r.HoraEntrada, r.HoraSalida, exigirFuturo: false);
            if (error is not null) return new(false, error);
            if (await HayEmpalmeAsync(r.FechaUso, r.HoraEntrada, r.HoraSalida, r.IdReservacion))
                return new(false, "No se puede autorizar: el horario ya está ocupado por otra reservación autorizada.");
        }

        var anterior = r.Estado;
        r.Estado = autorizar ? Estados.Autorizada : Estados.Rechazada;
        r.IdMaestroRevision = idMaestro;
        r.FechaRevision = DateTime.Now;
        r.ComentarioRevision = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        r.FechaModificacion = DateTime.Now;
        Registrar(r.IdReservacion, idMaestro, autorizar ? "autorizar" : "rechazar", new { estado = anterior }, new { estado = r.Estado, comentario = r.ComentarioRevision });
        Avisar(r, $"Tu solicitud #{r.IdReservacion} del {r.FechaUso:dd/MM/yyyy} fue {r.Estado}." + (r.ComentarioRevision is null ? "" : $" Comentario: {r.ComentarioRevision}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(true, $"Solicitud #{r.IdReservacion} {r.Estado}.");
    }

    // R07: solo el alumno propietario cancela una autorizada antes de la fecha de uso.
    public async Task<Resultado> CancelarAsync(int idReservacion, int idAlumno)
    {
        var r = await db.Reservaciones.FirstOrDefaultAsync(x => x.IdReservacion == idReservacion && x.IdAlumno == idAlumno);
        if (r is null) return new(false, "La reservación no existe o no es tuya.");
        if (r.Estado != Estados.Autorizada) return new(false, "Solo se pueden cancelar reservaciones autorizadas.");
        if (DateOnly.FromDateTime(DateTime.Now) >= r.FechaUso) return new(false, "Ya no se puede cancelar: la fecha de uso ya llegó.");

        r.Estado = Estados.Cancelada;
        r.FechaCancelacion = DateTime.Now;
        r.FechaModificacion = DateTime.Now;
        Registrar(r.IdReservacion, idAlumno, "cancelar", new { estado = Estados.Autorizada }, new { estado = r.Estado });
        Avisar(r, $"Cancelaste la reservación #{r.IdReservacion} del {r.FechaUso:dd/MM/yyyy}. El horario quedó libre.");
        await db.SaveChangesAsync();
        return new(true, $"Reservación #{r.IdReservacion} cancelada.");
    }

    // R08: entrada una sola vez en reservación autorizada; salida requiere entrada previa.
    public async Task<Resultado> RegistrarEntradaAsync(int idReservacion, int idMaestro)
    {
        var r = await db.Reservaciones.Include(x => x.Asistencia).FirstOrDefaultAsync(x => x.IdReservacion == idReservacion);
        if (r is null || r.Estado != Estados.Autorizada) return new(false, "Solo se registra entrada en reservaciones autorizadas.");
        if (r.Asistencia is not null) return new(false, "La entrada ya fue registrada.");
        var a = new Asistencia { IdReservacion = idReservacion, EntradaReal = DateTime.Now, IdMaestroEntrada = idMaestro };
        db.Asistencias.Add(a);
        Registrar(idReservacion, idMaestro, "registrar_entrada", null, new { entrada_real = a.EntradaReal });
        await db.SaveChangesAsync();
        return new(true, $"Entrada registrada a las {a.EntradaReal:HH:mm}.");
    }

    public async Task<Resultado> RegistrarSalidaAsync(int idReservacion, int idMaestro)
    {
        var a = await db.Asistencias.FirstOrDefaultAsync(x => x.IdReservacion == idReservacion);
        if (a is null) return new(false, "Primero registra la entrada.");
        if (a.SalidaReal is not null) return new(false, "La salida ya fue registrada.");
        a.SalidaReal = DateTime.Now;
        a.IdMaestroSalida = idMaestro;
        Registrar(idReservacion, idMaestro, "registrar_salida", null, new { salida_real = a.SalidaReal });
        await db.SaveChangesAsync();
        return new(true, $"Salida registrada a las {a.SalidaReal:HH:mm}.");
    }

    // R09: cada operación genera bitácora; nunca se guardan contraseñas ni tokens.
    private void Registrar(int idReservacion, int? actor, string accion, object? antes, object despues) =>
        db.Bitacora.Add(new BitacoraMovimiento
        {
            IdReservacion = idReservacion, IdActor = actor, Accion = accion,
            ValorAnterior = antes is null ? null : JsonSerializer.Serialize(antes),
            ValorNuevo = JsonSerializer.Serialize(despues)
        });

    private void Avisar(Reservacion r, string mensaje) =>
        db.Notificaciones.Add(new Notificacion
        {
            IdReservacion = r.IdReservacion, IdDestinatario = r.IdAlumno,
            EstadoNotificado = r.Estado, Mensaje = mensaje
        });
}
