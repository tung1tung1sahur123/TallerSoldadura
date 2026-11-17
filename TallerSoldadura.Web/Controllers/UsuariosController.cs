using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;

namespace TallerSoldadura.Web.Controllers;

// Catálogo de usuarios: registrar, modificar, consultar y desactivar.
[Authorize(Roles = "administrador")]
public class UsuariosController(TallerContext db, IPasswordHasher<Usuario> hasher) : BaseController
{
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Usuarios.Include(u => u.Roles).ThenInclude(r => r.Rol).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Nombre.Contains(q) || u.Apellidos.Contains(q) ||
                                     u.CorreoInstitucional.Contains(q) || (u.Matricula != null && u.Matricula.Contains(q)));
        ViewBag.Q = q;
        return View(await query.OrderBy(u => u.Apellidos).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        ViewBag.Roles = await db.Roles.OrderBy(r => r.IdRol).ToListAsync();
        return View("Formulario", new Usuario());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(Usuario datos, string contrasena, int[] roles)
    {
        var error = await ValidarAsync(datos, roles, nuevo: true, contrasena);
        if (error is not null) return await Volver(datos, error);

        var u = new Usuario
        {
            Nombre = datos.Nombre.Trim(), Apellidos = datos.Apellidos.Trim(),
            Matricula = string.IsNullOrWhiteSpace(datos.Matricula) ? null : datos.Matricula.Trim(),
            CorreoInstitucional = datos.CorreoInstitucional.Trim().ToLower(),
        };
        u.HashContrasena = hasher.HashPassword(u, contrasena);
        u.Roles = roles.Select(r => new UsuarioRol { IdRol = r }).ToList();
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        Mensaje(true, $"Usuario {u.NombreCompleto} registrado.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var u = await db.Usuarios.Include(x => x.Roles).FirstOrDefaultAsync(x => x.IdUsuario == id);
        if (u is null) return NotFound();
        ViewBag.Roles = await db.Roles.OrderBy(r => r.IdRol).ToListAsync();
        return View("Formulario", u);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, Usuario datos, string? contrasena, int[] roles)
    {
        var u = await db.Usuarios.Include(x => x.Roles).FirstOrDefaultAsync(x => x.IdUsuario == id);
        if (u is null) return NotFound();
        datos.IdUsuario = id;
        var error = await ValidarAsync(datos, roles, nuevo: false, contrasena);
        if (error is not null) return await Volver(datos, error);

        u.Nombre = datos.Nombre.Trim();
        u.Apellidos = datos.Apellidos.Trim();
        u.Matricula = string.IsNullOrWhiteSpace(datos.Matricula) ? null : datos.Matricula.Trim();
        u.CorreoInstitucional = datos.CorreoInstitucional.Trim().ToLower();
        if (!string.IsNullOrWhiteSpace(contrasena)) u.HashContrasena = hasher.HashPassword(u, contrasena);
        u.FechaModificacion = DateTime.Now;
        u.Roles.RemoveAll(r => !roles.Contains(r.IdRol));
        foreach (var r in roles.Where(r => u.Roles.All(x => x.IdRol != r))) u.Roles.Add(new UsuarioRol { IdRol = r });
        await db.SaveChangesAsync();
        Mensaje(true, $"Usuario {u.NombreCompleto} actualizado.");
        return RedirectToAction(nameof(Index));
    }

    // Desactivar (o reactivar). Nunca se borra la cuenta, para conservar su historial.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return NotFound();
        if (u.IdUsuario == IdUsuario) { Mensaje(false, "No puedes desactivar tu propia cuenta."); return RedirectToAction(nameof(Index)); }
        u.Activo = !u.Activo;
        u.FechaModificacion = DateTime.Now;
        await db.SaveChangesAsync();
        Mensaje(true, $"Cuenta de {u.NombreCompleto} {(u.Activo ? "reactivada" : "desactivada")}.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> ValidarAsync(Usuario d, int[] roles, bool nuevo, string? contrasena)
    {
        if (string.IsNullOrWhiteSpace(d.Nombre) || string.IsNullOrWhiteSpace(d.Apellidos) || string.IsNullOrWhiteSpace(d.CorreoInstitucional))
            return "Nombre, apellidos y correo son obligatorios.";
        if (nuevo && (contrasena?.Length ?? 0) < 8) return "La contraseña debe tener al menos 8 caracteres.";
        if (roles.Length == 0) return "Asigna al menos un rol.";
        var correo = d.CorreoInstitucional.Trim().ToLower();
        if (await db.Usuarios.AnyAsync(u => u.CorreoInstitucional == correo && u.IdUsuario != d.IdUsuario))
            return "Ese correo institucional ya está registrado.";
        var idAlumno = await db.Roles.Where(r => r.NombreRol == "alumno").Select(r => r.IdRol).FirstOrDefaultAsync();
        if (roles.Contains(idAlumno) && string.IsNullOrWhiteSpace(d.Matricula)) return "El rol alumno requiere matrícula.";
        if (!string.IsNullOrWhiteSpace(d.Matricula) &&
            await db.Usuarios.AnyAsync(u => u.Matricula == d.Matricula.Trim() && u.IdUsuario != d.IdUsuario))
            return "Esa matrícula ya está registrada.";
        return null;
    }

    private async Task<IActionResult> Volver(Usuario datos, string error)
    {
        ViewData["Error"] = error;
        ViewBag.Roles = await db.Roles.OrderBy(r => r.IdRol).ToListAsync();
        return View("Formulario", datos);
    }
}
