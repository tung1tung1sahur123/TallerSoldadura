using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;

namespace TallerSoldadura.Web.Controllers;

public class CuentaController(TallerContext db, IPasswordHasher<Usuario> hasher) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string correo, string contrasena, string? returnUrl = null)
    {
        var u = await db.Usuarios.Include(x => x.Roles).ThenInclude(r => r.Rol)
                                 .FirstOrDefaultAsync(x => x.CorreoInstitucional == correo);
        bool valida = u is not null && u.Activo &&
                      hasher.VerifyHashedPassword(u, u.HashContrasena, contrasena ?? "") != PasswordVerificationResult.Failed;
        if (!valida)
        {
            ViewData["Error"] = "Correo o contraseña incorrectos, o la cuenta está desactivada.";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u!.IdUsuario.ToString()),
            new(ClaimTypes.Name, u.NombreCompleto),
            new(ClaimTypes.Email, u.CorreoInstitucional),
        };
        claims.AddRange(u.Roles.Select(r => new Claim(ClaimTypes.Role, r.Rol.NombreRol)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccesoDenegado() => View();
}
