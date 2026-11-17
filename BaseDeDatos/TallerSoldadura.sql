/* =====================================================================
   Actividad 9.1  Diseño de la base de datos del sistema
   Sistema de reservación del taller de soldadura - Universidad del Noreste
   Integrantes: Victor Isaac Yañez Cervantes, Angel Oswaldo Martínez Hernández
   Grupo 4L  ·  SMBD: Microsoft SQL Server
   Basado en el diccionario de datos (Actividad 8.1, D01-D10)
   ===================================================================== */

USE master;
GO
IF DB_ID('TallerSoldadura') IS NOT NULL
BEGIN
    ALTER DATABASE TallerSoldadura SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TallerSoldadura;
END
GO
CREATE DATABASE TallerSoldadura;
GO
USE TallerSoldadura;
GO

/* ---------------------------- CATÁLOGOS ---------------------------- */

-- D01 roles
CREATE TABLE roles (
    id_rol        INT IDENTITY(1,1) NOT NULL,
    nombre_rol    VARCHAR(20)  NOT NULL,
    descripcion   VARCHAR(150) NOT NULL,
    CONSTRAINT PK_roles PRIMARY KEY (id_rol),
    CONSTRAINT UQ_roles_nombre UNIQUE (nombre_rol),
    CONSTRAINT CK_roles_nombre CHECK (nombre_rol IN ('alumno', 'maestro', 'administrador'))
);
GO

-- D02 usuarios
CREATE TABLE usuarios (
    id_usuario            INT IDENTITY(1,1) NOT NULL,
    nombre                VARCHAR(80)  NOT NULL,
    apellidos             VARCHAR(120) NOT NULL,
    matricula             VARCHAR(20)  NULL,
    correo_institucional  VARCHAR(254) NOT NULL,
    hash_contrasena       VARCHAR(255) NOT NULL,
    activo                BIT          NOT NULL CONSTRAINT DF_usuarios_activo DEFAULT (1),
    fecha_alta            DATETIME2(0) NOT NULL CONSTRAINT DF_usuarios_alta DEFAULT (SYSDATETIME()),
    fecha_modificacion    DATETIME2(0) NOT NULL CONSTRAINT DF_usuarios_modif DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_usuarios PRIMARY KEY (id_usuario),
    CONSTRAINT UQ_usuarios_correo UNIQUE (correo_institucional),
    CONSTRAINT CK_usuarios_nombre CHECK (LEN(LTRIM(RTRIM(nombre))) > 0),
    CONSTRAINT CK_usuarios_correo CHECK (correo_institucional LIKE '%_@_%._%')
);
GO
-- La matrícula es única solo cuando existe (índice filtrado)
CREATE UNIQUE INDEX UQ_usuarios_matricula ON usuarios (matricula) WHERE matricula IS NOT NULL;
GO

-- D03 usuario_rol  (resuelve la relación muchos a muchos usuarios - roles)
CREATE TABLE usuario_rol (
    id_usuario        INT          NOT NULL,
    id_rol            INT          NOT NULL,
    fecha_asignacion  DATETIME2(0) NOT NULL CONSTRAINT DF_usuario_rol_fecha DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_usuario_rol PRIMARY KEY (id_usuario, id_rol),
    CONSTRAINT FK_usuario_rol_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios (id_usuario),
    CONSTRAINT FK_usuario_rol_rol     FOREIGN KEY (id_rol)     REFERENCES roles (id_rol)
);
GO

-- D04 horarios_apertura
CREATE TABLE horarios_apertura (
    id_horario     INT IDENTITY(1,1) NOT NULL,
    dia_semana     SMALLINT NOT NULL,
    hora_apertura  TIME(0)  NOT NULL,
    hora_cierre    TIME(0)  NOT NULL,
    activo         BIT      NOT NULL CONSTRAINT DF_horarios_activo DEFAULT (1),
    CONSTRAINT PK_horarios_apertura PRIMARY KEY (id_horario),
    CONSTRAINT UQ_horarios_franja UNIQUE (dia_semana, hora_apertura, hora_cierre),
    CONSTRAINT CK_horarios_dia   CHECK (dia_semana BETWEEN 1 AND 7),   -- lunes = 1 ... domingo = 7
    CONSTRAINT CK_horarios_horas CHECK (hora_cierre > hora_apertura)
);
GO

/* ---------------------------- OPERACIÓN ---------------------------- */

-- D05 reservaciones
CREATE TABLE reservaciones (
    id_reservacion        INT IDENTITY(1,1) NOT NULL,          -- también es el folio del comprobante
    id_alumno             INT          NOT NULL,
    fecha_uso             DATE         NOT NULL,
    hora_entrada          TIME(0)      NOT NULL,
    hora_salida           TIME(0)      NOT NULL,
    estado                VARCHAR(12)  NOT NULL CONSTRAINT DF_reservaciones_estado DEFAULT ('pendiente'),
    fecha_solicitud       DATETIME2(0) NOT NULL CONSTRAINT DF_reservaciones_solicitud DEFAULT (SYSDATETIME()),
    id_maestro_revision   INT          NULL,
    fecha_revision        DATETIME2(0) NULL,
    comentario_revision   VARCHAR(500) NULL,
    fecha_cancelacion     DATETIME2(0) NULL,
    fecha_modificacion    DATETIME2(0) NOT NULL CONSTRAINT DF_reservaciones_modif DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_reservaciones PRIMARY KEY (id_reservacion),
    CONSTRAINT FK_reservaciones_alumno  FOREIGN KEY (id_alumno)           REFERENCES usuarios (id_usuario),
    CONSTRAINT FK_reservaciones_maestro FOREIGN KEY (id_maestro_revision) REFERENCES usuarios (id_usuario),
    CONSTRAINT CK_reservaciones_horas  CHECK (hora_salida > hora_entrada),
    CONSTRAINT CK_reservaciones_estado CHECK (estado IN ('pendiente', 'autorizada', 'rechazada', 'cancelada')),
    -- R07: autorizar o rechazar exige maestro y fecha de revisión
    CONSTRAINT CK_reservaciones_revision CHECK (
        estado = 'pendiente'
        OR (id_maestro_revision IS NOT NULL AND fecha_revision IS NOT NULL)
        OR (estado = 'cancelada')),
    -- fecha_cancelacion solo cuando estado = cancelada
    CONSTRAINT CK_reservaciones_cancelacion CHECK (
        (estado = 'cancelada' AND fecha_cancelacion IS NOT NULL)
        OR (estado <> 'cancelada' AND fecha_cancelacion IS NULL))
);
GO
CREATE INDEX IX_reservaciones_fecha  ON reservaciones (fecha_uso, estado);
CREATE INDEX IX_reservaciones_alumno ON reservaciones (id_alumno);
GO

-- D06 asistencias  (entidad débil: depende de la reservación)
CREATE TABLE asistencias (
    id_reservacion      INT          NOT NULL,
    entrada_real        DATETIME2(0) NOT NULL,
    salida_real         DATETIME2(0) NULL,
    id_maestro_entrada  INT          NOT NULL,
    id_maestro_salida   INT          NULL,
    CONSTRAINT PK_asistencias PRIMARY KEY (id_reservacion),
    CONSTRAINT FK_asistencias_reservacion     FOREIGN KEY (id_reservacion)     REFERENCES reservaciones (id_reservacion),
    CONSTRAINT FK_asistencias_maestro_entrada FOREIGN KEY (id_maestro_entrada) REFERENCES usuarios (id_usuario),
    CONSTRAINT FK_asistencias_maestro_salida  FOREIGN KEY (id_maestro_salida)  REFERENCES usuarios (id_usuario),
    CONSTRAINT CK_asistencias_salida CHECK (salida_real IS NULL OR salida_real > entrada_real),
    CONSTRAINT CK_asistencias_maestro_salida CHECK (
        (salida_real IS NULL AND id_maestro_salida IS NULL)
        OR (salida_real IS NOT NULL AND id_maestro_salida IS NOT NULL))
);
GO

-- D07 notificaciones
CREATE TABLE notificaciones (
    id_notificacion    INT IDENTITY(1,1) NOT NULL,
    id_reservacion     INT          NOT NULL,
    id_destinatario    INT          NOT NULL,
    estado_notificado  VARCHAR(12)  NOT NULL,
    mensaje            VARCHAR(500) NOT NULL,
    fecha_creacion     DATETIME2(0) NOT NULL CONSTRAINT DF_notificaciones_creacion DEFAULT (SYSDATETIME()),
    fecha_lectura      DATETIME2(0) NULL,                       -- NULL = no leído
    CONSTRAINT PK_notificaciones PRIMARY KEY (id_notificacion),
    CONSTRAINT FK_notificaciones_reservacion  FOREIGN KEY (id_reservacion)  REFERENCES reservaciones (id_reservacion),
    CONSTRAINT FK_notificaciones_destinatario FOREIGN KEY (id_destinatario) REFERENCES usuarios (id_usuario),
    CONSTRAINT CK_notificaciones_estado  CHECK (estado_notificado IN ('pendiente', 'autorizada', 'rechazada', 'cancelada')),
    CONSTRAINT CK_notificaciones_mensaje CHECK (LEN(LTRIM(RTRIM(mensaje))) > 0),
    CONSTRAINT CK_notificaciones_lectura CHECK (fecha_lectura IS NULL OR fecha_lectura >= fecha_creacion)
);
GO

-- D08 bitacora_movimientos
CREATE TABLE bitacora_movimientos (
    id_movimiento   INT IDENTITY(1,1) NOT NULL,
    id_reservacion  INT          NOT NULL,
    id_actor        INT          NULL,                          -- NULL = acción automática del sistema
    accion          VARCHAR(20)  NOT NULL,
    fecha_evento    DATETIME2(0) NOT NULL CONSTRAINT DF_bitacora_fecha DEFAULT (SYSDATETIME()),
    valor_anterior  VARCHAR(MAX) NULL,
    valor_nuevo     VARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_bitacora_movimientos PRIMARY KEY (id_movimiento),
    CONSTRAINT FK_bitacora_reservacion FOREIGN KEY (id_reservacion) REFERENCES reservaciones (id_reservacion),
    CONSTRAINT FK_bitacora_actor       FOREIGN KEY (id_actor)       REFERENCES usuarios (id_usuario),
    CONSTRAINT CK_bitacora_accion CHECK (accion IN ('crear', 'autorizar', 'rechazar', 'modificar',
                                                    'cancelar', 'registrar_entrada', 'registrar_salida'))
);
GO

/* ----------------------------- SOPORTE ----------------------------- */

-- D09 sesiones
CREATE TABLE sesiones (
    hash_token        CHAR(64)     NOT NULL,
    id_usuario        INT          NOT NULL,
    fecha_inicio      DATETIME2(0) NOT NULL CONSTRAINT DF_sesiones_inicio DEFAULT (SYSDATETIME()),
    ultima_actividad  DATETIME2(0) NOT NULL CONSTRAINT DF_sesiones_actividad DEFAULT (SYSDATETIME()),
    revocada          BIT          NOT NULL CONSTRAINT DF_sesiones_revocada DEFAULT (0),
    CONSTRAINT PK_sesiones PRIMARY KEY (hash_token),
    CONSTRAINT FK_sesiones_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios (id_usuario),
    CONSTRAINT CK_sesiones_actividad CHECK (ultima_actividad >= fecha_inicio)
);
GO

-- D10 respaldos
CREATE TABLE respaldos (
    id_respaldo    INT IDENTITY(1,1) NOT NULL,
    fecha_inicio   DATETIME2(0)  NOT NULL CONSTRAINT DF_respaldos_inicio DEFAULT (SYSDATETIME()),
    fecha_fin      DATETIME2(0)  NULL,
    estado         VARCHAR(12)   NOT NULL CONSTRAINT DF_respaldos_estado DEFAULT ('en_proceso'),
    ubicacion      VARCHAR(255)  NULL,
    tamano_bytes   BIGINT        NULL,
    detalle_error  VARCHAR(1000) NULL,
    CONSTRAINT PK_respaldos PRIMARY KEY (id_respaldo),
    CONSTRAINT CK_respaldos_estado CHECK (estado IN ('en_proceso', 'exitoso', 'fallido')),
    CONSTRAINT CK_respaldos_fin    CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio),
    CONSTRAINT CK_respaldos_exitoso CHECK (estado <> 'exitoso'
        OR (fecha_fin IS NOT NULL AND ubicacion IS NOT NULL AND tamano_bytes > 0)),
    CONSTRAINT CK_respaldos_fallido CHECK (estado <> 'fallido'
        OR (fecha_fin IS NOT NULL AND detalle_error IS NOT NULL))
);
GO

/* ------------------------- DATOS DE PRUEBA ------------------------- */

INSERT INTO roles (nombre_rol, descripcion) VALUES
 ('alumno',        'Solicita, consulta y cancela sus reservaciones del taller'),
 ('maestro',       'Revisa solicitudes y registra la asistencia real de los alumnos'),
 ('administrador', 'Gestiona usuarios, roles y horarios de apertura del taller');

INSERT INTO usuarios (nombre, apellidos, matricula, correo_institucional, hash_contrasena) VALUES
 ('Angel Oswaldo', 'Martínez Hernández', '20240001', 'angel.martinez@une.edu.mx',  'hash_demo_1'),
 ('Victor Isaac',  'Yañez Cervantes',    '20240002', 'victor.yanez@une.edu.mx',    'hash_demo_2'),
 ('Maestro',       'Encargado Taller',   NULL,       'taller.soldadura@une.edu.mx','hash_demo_3'),
 ('Admin',         'Sistema',            NULL,       'admin.sistemas@une.edu.mx',  'hash_demo_4');

INSERT INTO usuario_rol (id_usuario, id_rol) VALUES (1, 1), (2, 1), (3, 2), (4, 3);

INSERT INTO horarios_apertura (dia_semana, hora_apertura, hora_cierre) VALUES
 (1, '08:00', '14:00'), (2, '08:00', '14:00'), (3, '08:00', '14:00'),
 (4, '08:00', '14:00'), (5, '08:00', '13:00');

INSERT INTO reservaciones (id_alumno, fecha_uso, hora_entrada, hora_salida, estado,
                           id_maestro_revision, fecha_revision, comentario_revision) VALUES
 (1, '2026-11-10', '09:00', '11:00', 'autorizada', 3, '2026-11-08 10:00', 'Traer equipo de protección'),
 (2, '2026-11-10', '11:00', '12:30', 'pendiente',  NULL, NULL, NULL),
 (2, '2026-11-11', '09:00', '10:00', 'rechazada',  3, '2026-11-08 10:05', 'Taller ocupado por mantenimiento');

INSERT INTO asistencias (id_reservacion, entrada_real, salida_real, id_maestro_entrada, id_maestro_salida)
VALUES (1, '2026-11-10 09:05', '2026-11-10 10:58', 3, 3);

INSERT INTO notificaciones (id_reservacion, id_destinatario, estado_notificado, mensaje) VALUES
 (1, 1, 'autorizada', 'Tu solicitud del 10/11/2026 de 09:00 a 11:00 fue autorizada.'),
 (3, 2, 'rechazada',  'Tu solicitud del 11/11/2026 de 09:00 a 10:00 fue rechazada.');

INSERT INTO bitacora_movimientos (id_reservacion, id_actor, accion, valor_anterior, valor_nuevo) VALUES
 (1, 1, 'crear',     NULL, '{"estado":"pendiente"}'),
 (1, 3, 'autorizar', '{"estado":"pendiente"}', '{"estado":"autorizada"}'),
 (2, 2, 'crear',     NULL, '{"estado":"pendiente"}'),
 (3, 2, 'crear',     NULL, '{"estado":"pendiente"}'),
 (3, 3, 'rechazar',  '{"estado":"pendiente"}', '{"estado":"rechazada"}'),
 (1, 3, 'registrar_entrada', NULL, '{"entrada_real":"2026-11-10 09:05"}'),
 (1, 3, 'registrar_salida',  NULL, '{"salida_real":"2026-11-10 10:58"}');

INSERT INTO sesiones (hash_token, id_usuario)
VALUES (REPLICATE('a', 64), 1);

INSERT INTO respaldos (fecha_inicio, fecha_fin, estado, ubicacion, tamano_bytes)
VALUES ('2026-11-08 02:00', '2026-11-08 02:03', 'exitoso', 'respaldos/TallerSoldadura_20261108.bak', 5242880);
GO

/* --------------------- CONSULTA DE COMPROBACIÓN -------------------- */
-- Historial de uso del taller: quién lo usó, cuándo y cuánto tiempo
SELECT r.id_reservacion AS folio,
       u.nombre + ' ' + u.apellidos AS alumno,
       u.matricula,
       r.fecha_uso,
       a.entrada_real,
       a.salida_real,
       DATEDIFF(MINUTE, a.entrada_real, a.salida_real) AS duracion_minutos
FROM reservaciones r
JOIN usuarios u    ON u.id_usuario = r.id_alumno
JOIN asistencias a ON a.id_reservacion = r.id_reservacion;
GO
