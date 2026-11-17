using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallerSoldadura.Web.Controllers;

[Authorize]
public class HomeController : BaseController
{
    // Página de inicio: muestra los accesos según el rol del usuario.
    public IActionResult Index() => View();

    [AllowAnonymous]
    public IActionResult Error() => View();
}
