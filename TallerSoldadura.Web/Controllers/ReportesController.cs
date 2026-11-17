using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;

namespace TallerSoldadura.Web.Controllers;

// Reportes: historial de uso (maestro y administrador), exportación y bitácora (administrador).
[Authorize(Roles = "maestro,administrador")]
public class ReportesController(TallerContext db) : BaseController
{
    private async Task<List<Asistencia>> UsosAsync(DateOnly desde, DateOnly hasta)
    {
        var ini = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await db.Asistencias.Include(a => a.Reservacion).ThenInclude(r => r.Alumno)
                       .Where(a => a.EntradaReal >= ini && a.EntradaReal < fin)
                       .OrderBy(a => a.EntradaReal).ToListAsync();
    }

    public async Task<IActionResult> HistorialUso(DateOnly? desde, DateOnly? hasta)
    {
        var d = desde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var h = hasta ?? DateOnly.FromDateTime(DateTime.Today);
        if (d > h) { Mensaje(false, "La fecha inicial no puede ser mayor que la final."); (d, h) = (h, d); }
        ViewBag.Desde = d; ViewBag.Hasta = h;
        return View(await UsosAsync(d, h));
    }

    // Exporta el historial de uso a CSV (se abre en Excel).
    [Authorize(Roles = "maestro")]
    public async Task<IActionResult> Exportar(DateOnly desde, DateOnly hasta)
    {
        var usos = await UsosAsync(desde, hasta);
        if (usos.Count == 0)
        {
            Mensaje(false, "No hay usos registrados en ese periodo.");
            return RedirectToAction(nameof(HistorialUso), new { desde = desde.ToString("yyyy-MM-dd"), hasta = hasta.ToString("yyyy-MM-dd") });
        }
        var sb = new StringBuilder("Folio,Alumno,Matricula,Fecha,Entrada real,Salida real,Duracion (min)\n");
        foreach (var a in usos)
            sb.AppendLine(string.Join(",", a.IdReservacion, $"\"{a.Reservacion.Alumno.NombreCompleto}\"", a.Reservacion.Alumno.Matricula,
                a.Reservacion.FechaUso.ToString("yyyy-MM-dd"), a.EntradaReal.ToString("HH:mm"),
                a.SalidaReal?.ToString("HH:mm") ?? "pendiente",
                a.DuracionMinutos?.ToString("0.00", CultureInfo.InvariantCulture) ?? ""));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"uso_taller_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "administrador")]
    public async Task<IActionResult> Bitacora(DateOnly? desde, DateOnly? hasta)
    {
        var d = desde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var h = hasta ?? DateOnly.FromDateTime(DateTime.Today);
        ViewBag.Desde = d; ViewBag.Hasta = h;
        var ini = d.ToDateTime(TimeOnly.MinValue);
        var fin = h.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return View(await db.Bitacora.Include(b => b.Actor)
                            .Where(b => b.FechaEvento >= ini && b.FechaEvento < fin)
                            .OrderBy(b => b.FechaEvento).ToListAsync());
    }
}
