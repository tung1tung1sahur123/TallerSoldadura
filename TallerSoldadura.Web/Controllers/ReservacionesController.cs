using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;
using TallerSoldadura.Web.Services;

namespace TallerSoldadura.Web.Controllers;

// Proceso del alumno: calendario, solicitud, consulta de estado, cancelación, comprobante y avisos.
[Authorize(Roles = "alumno")]
public class ReservacionesController(TallerContext db, ReservacionService servicio) : BaseController
{
    // Calendario de horas apartadas y disponibles para una fecha.
    public async Task<IActionResult> Calendario(DateOnly? fecha)
    {
        var dia = fecha ?? DateOnly.FromDateTime(DateTime.Today);
        short ds = (short)(dia.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dia.DayOfWeek);
        ViewBag.Fecha = dia;
        ViewBag.Franjas = await db.Horarios.Where(h => h.Activo && h.DiaSemana == ds).OrderBy(h => h.HoraApertura).ToListAsync();
        ViewBag.MisPendientes = await db.Reservaciones
            .Where(r => r.FechaUso == dia && r.IdAlumno == IdUsuario && r.Estado == Estados.Pendiente)
            .OrderBy(r => r.HoraEntrada).ToListAsync();
        var ocupadas = await db.Reservaciones
            .Where(r => r.FechaUso == dia && r.Estado == Estados.Autorizada)
            .OrderBy(r => r.HoraEntrada).ToListAsync();
        return View(ocupadas);
    }

    [HttpGet]
    public IActionResult Nueva(DateOnly? fecha) =>
        View(new Reservacion { FechaUso = fecha ?? DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                               HoraEntrada = new TimeOnly(9, 0), HoraSalida = new TimeOnly(10, 0) });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(DateOnly fechaUso, TimeOnly horaEntrada, TimeOnly horaSalida)
    {
        var res = await servicio.SolicitarAsync(IdUsuario, fechaUso, horaEntrada, horaSalida);
        if (!res.Ok)
        {
            ViewData["Error"] = res.Mensaje;
            return View(new Reservacion { FechaUso = fechaUso, HoraEntrada = horaEntrada, HoraSalida = horaSalida });
        }
        Mensaje(true, res.Mensaje);
        return RedirectToAction(nameof(Mias));
    }

    // Historial de reservaciones del alumno con su estado.
    public async Task<IActionResult> Mias() =>
        View(await db.Reservaciones.Where(r => r.IdAlumno == IdUsuario)
                     .OrderByDescending(r => r.FechaUso).ThenByDescending(r => r.HoraEntrada).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var res = await servicio.CancelarAsync(id, IdUsuario);
        Mensaje(res.Ok, res.Mensaje);
        return RedirectToAction(nameof(Mias));
    }

    // Comprobante de reservación autorizada (se verifica el estado vigente al consultarlo).
    public async Task<IActionResult> Comprobante(int id)
    {
        var r = await db.Reservaciones.Include(x => x.Alumno)
                        .FirstOrDefaultAsync(x => x.IdReservacion == id && x.IdAlumno == IdUsuario);
        if (r is null || r.Estado != Estados.Autorizada)
        {
            Mensaje(false, "Solo hay comprobante para reservaciones autorizadas.");
            return RedirectToAction(nameof(Mias));
        }
        return View(r);
    }

    // Avisos internos; al abrirlos se marcan como leídos.
    public async Task<IActionResult> Avisos()
    {
        var avisos = await db.Notificaciones.Where(n => n.IdDestinatario == IdUsuario)
                             .OrderByDescending(n => n.FechaCreacion).ToListAsync();
        foreach (var n in avisos.Where(n => n.FechaLectura is null)) n.FechaLectura = DateTime.Now;
        await db.SaveChangesAsync();
        return View(avisos);
    }
}
