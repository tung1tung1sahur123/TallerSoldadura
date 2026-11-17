using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;
using TallerSoldadura.Web.Services;

namespace TallerSoldadura.Web.Controllers;

// Proceso del maestro: revisión de solicitudes y registro de asistencia.
[Authorize(Roles = "maestro")]
public class MaestroController(TallerContext db, ReservacionService servicio) : BaseController
{
    public async Task<IActionResult> Pendientes() =>
        View(await db.Reservaciones.Include(r => r.Alumno)
                     .Where(r => r.Estado == Estados.Pendiente)
                     .OrderBy(r => r.FechaUso).ThenBy(r => r.HoraEntrada).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revisar(int id, bool autorizar, string? comentario)
    {
        var res = await servicio.RevisarAsync(id, IdUsuario, autorizar, comentario);
        Mensaje(res.Ok, res.Mensaje);
        return RedirectToAction(nameof(Pendientes));
    }

    // Reservaciones autorizadas del día, con su asistencia.
    public async Task<IActionResult> DelDia(DateOnly? fecha)
    {
        var dia = fecha ?? DateOnly.FromDateTime(DateTime.Today);
        ViewBag.Fecha = dia;
        return View(await db.Reservaciones.Include(r => r.Alumno).Include(r => r.Asistencia)
                            .Where(r => r.FechaUso == dia && r.Estado == Estados.Autorizada)
                            .OrderBy(r => r.HoraEntrada).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrada(int id, DateOnly fecha)
    {
        var res = await servicio.RegistrarEntradaAsync(id, IdUsuario);
        Mensaje(res.Ok, res.Mensaje);
        return RedirectToAction(nameof(DelDia), new { fecha = fecha.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Salida(int id, DateOnly fecha)
    {
        var res = await servicio.RegistrarSalidaAsync(id, IdUsuario);
        Mensaje(res.Ok, res.Mensaje);
        return RedirectToAction(nameof(DelDia), new { fecha = fecha.ToString("yyyy-MM-dd") });
    }
}
