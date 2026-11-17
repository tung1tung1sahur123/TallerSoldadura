using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TallerSoldadura.Web.Controllers;

public abstract class BaseController : Controller
{
    protected int IdUsuario => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected void Mensaje(bool ok, string texto)
    {
        TempData["ok"] = ok;
        TempData["msg"] = texto;
    }
}
