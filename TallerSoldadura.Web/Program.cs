using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerSoldadura.Web.Data;
using TallerSoldadura.Web.Models;
using TallerSoldadura.Web.Services;

// Registro de diagnóstico: escribe errores de arranque en "registro_arranque.txt" junto al proyecto.
var registro = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "registro_arranque.txt");
void Registrar(string texto) { try { File.AppendAllText(registro, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {texto}{Environment.NewLine}"); } catch { } }
AppDomain.CurrentDomain.UnhandledException += (_, e) => Registrar("ERROR NO CONTROLADO: " + e.ExceptionObject);
Registrar("Iniciando aplicación...");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<TallerContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("TallerSoldadura")));
builder.Services.AddScoped<ReservacionService>();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// Inicio de sesión con cookie: expira tras 30 minutos de inactividad (R10).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Cuenta/Login";
        o.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Solo en desarrollo: las cuentas de prueba del script SQL traen "hash_demo_N".
    // Se reemplazan por el hash real de la contraseña "Taller123!" para poder entrar.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TallerContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
    try
    {
        var demo = db.Usuarios.Where(u => u.HashContrasena.StartsWith("hash_demo")).ToList();
        foreach (var u in demo) u.HashContrasena = hasher.HashPassword(u, "Taller123!");
        if (demo.Count > 0) db.SaveChanges();
        Registrar($"Conexión a SQL Server correcta. Usuarios en la base: {db.Usuarios.Count()}");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "No se pudo conectar a SQL Server. Revisa la cadena de conexión en appsettings.json.");
        Registrar("ERROR DE CONEXIÓN A SQL SERVER: " + ex.Message);
    }
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Lifetime.ApplicationStarted.Register(() => Registrar("Aplicación lista en: " + string.Join(", ", app.Urls)));
try { app.Run(); }
catch (Exception ex) { Registrar("ERROR AL ARRANCAR: " + ex); throw; }
