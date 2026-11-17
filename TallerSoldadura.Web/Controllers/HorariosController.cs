using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;

namespace TallerSoldadura.Web.Controllers;

// Catálogo de horarios de apertura del taller.
[Authorize(Roles = "administrador")]
public class HorariosController(TallerContext db) : BaseController
{
    public async Task<IActionResult> Index() =>
        View(await db.Horarios.OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraApertura).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(short diaSemana, TimeOnly horaApertura, TimeOnly horaCierre)
    {
        if (diaSemana is < 1 or > 7 || horaCierre <= horaApertura)
            Mensaje(false, "Revisa el día y que la hora de cierre sea mayor que la de apertura.");
        else if (await db.Horarios.AnyAsync(h => h.Activo && h.DiaSemana == diaSemana && horaApertura < h.HoraCierre && horaCierre > h.HoraApertura))
            Mensaje(false, "La franja se superpone con otra franja activa del mismo día.");
        else
        {
            db.Horarios.Add(new HorarioApertura { DiaSemana = diaSemana, HoraApertura = horaApertura, HoraCierre = horaCierre });
            await db.SaveChangesAsync();
            Mensaje(true, "Franja de apertura agregada.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var h = await db.Horarios.FindAsync(id);
        if (h is null) return NotFound();
        h.Activo = !h.Activo;
        await db.SaveChangesAsync();

        // Al desactivar una franja se avisa cuántas reservaciones futuras autorizadas quedan fuera de horario.
        if (!h.Activo)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var futuras = await db.Reservaciones.Where(r => r.Estado == Estados.Autorizada && r.FechaUso >= hoy).ToListAsync();
            int afectadas = futuras.Count(r =>
                ((short)(r.FechaUso.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)r.FechaUso.DayOfWeek)) == h.DiaSemana &&
                r.HoraEntrada >= h.HoraApertura && r.HoraSalida <= h.HoraCierre);
            Mensaje(true, afectadas == 0 ? "Franja desactivada." :
                $"Franja desactivada. Atención: hay {afectadas} reservación(es) autorizada(s) futuras en esa franja; revísalas con el maestro.");
        }
        else Mensaje(true, "Franja activada.");
        return RedirectToAction(nameof(Index));
    }
}
