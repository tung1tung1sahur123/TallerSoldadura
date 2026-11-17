# Sistema de reservación del taller de soldadura (ASP.NET Core + SQL Server)

## Cómo ejecutarlo
1. En SQL Server Management Studio abre `BaseDeDatos/TallerSoldadura.sql` y ejecútalo (F5). Crea la base `TallerSoldadura` con datos de prueba.
2. Abre `TallerSoldadura.slnx` en Visual Studio.
3. Si tu servidor no es `localhost\SQLEXPRESS`, cambia la cadena de conexión en `TallerSoldadura.Web/appsettings.json`.
4. Ejecuta con F5. Se abre el navegador en la pantalla de inicio de sesión.

## Cuentas de prueba (contraseña: Taller123!)
| Rol | Correo |
| --- | --- |
| Alumno | angel.martinez@une.edu.mx |
| Alumno | victor.yanez@une.edu.mx |
| Maestro | taller.soldadura@une.edu.mx |
| Administrador | admin.sistemas@une.edu.mx |

La primera vez que se ejecuta en modo Desarrollo, el programa cambia los valores `hash_demo_N` del script por el hash real de `Taller123!`.

## Qué incluye
- **Alumno:** calendario, nueva solicitud (valida horario de apertura y empalmes), mis reservaciones, cancelación, comprobante imprimible y avisos.
- **Maestro:** solicitudes pendientes (autorizar/rechazar con comentario), reservaciones del día con registro de entrada y salida real, historial de uso y exportación a CSV (Excel).
- **Administrador:** usuarios (registrar, modificar, desactivar, roles), horarios de apertura, bitácora de movimientos e historial de uso.

## Estructura
- `Models/Entidades.cs`: clases de las tablas.
- `Data/TallerContext.cs`: mapeo de Entity Framework a las tablas de SQL Server.
- `Services/ReservacionService.cs`: reglas del proceso (empalmes, estados, bitácora, avisos).
- `Controllers/` y `Views/`: pantallas por rol.
