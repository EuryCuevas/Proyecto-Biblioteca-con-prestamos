# Diseño de componentes — Core Biblioteca

Este documento es la **semana 2**. Explica qué hace cada componente, qué operaciones
expone y por qué se toman las decisiones que se toman.

> **Alcance.** Este documento describe el estado del andamiaje (lo construido hasta ahora)
> y deja indicadas las decisiones que se aplicarán en las prácticas siguientes. No
> describe functionality que aún no existe: cuando una pieza crezca, esta tabla se
> actualiza.

---

## Cómo leer este documento

1. La sección 2 es el **mapa**: un componente por fila, con su responsabilidad y su
   interfaz. Si sólo tienes un minuto, lee esa tabla.
2. La sección 3 explica las **reglas de dependencia**, que son la parte defendible: quién
   puede saber de quién.
3. La sección 4 justifica las **decisiones técnicas** que hay que defender en la revisión.
4. La sección 5 dice explícitamente **qué NO incluye** cada componente, porque una
   revisión penaliza tanto el exceso como la omisión.

---

## 1. Principio rector: el Core no conoce al módulo de negocio

**RD-03** es la regla que gobierna todo el diseño:

> El Core no depende del módulo de negocio.

Se cumple de tres formas independientes y comprobables:

| Comprobación | Cómo se verifica |
|---|---|
| Ningún archivo del Core importa `Biblioteca.Biblioteca` | `grep -r "Biblioteca.Biblioteca" src/Biblioteca.Nucleo src/Biblioteca.Identidad src/Biblioteca.Correo` → sin resultados |
| El Core compira y arranca sin el módulo de negocio | Quitar la referencia al proyecto `Biblioteca.Biblioteca` y ejecutar `dotnet build` |
| La máquina de estados del negocio es independiente de la de permisos | Las dos declaraciones viven en archivos separados, sin punto común (RF-NEG-09) |

En Core, el negocio **depende** del Core; el Core **no sabe** que el negocio existe.

---

## 2. Mapa de componentes (RD-01)

Cada pieza del Core es un componente con **una sola responsabilidad** y una **interfaz
explícita**. En el repositorio eso es literalmente un proyecto de .NET por pieza.

### 2.1 Núcleo transversal — `src/Biblioteca.Nucleo`

| | |
|---|---|
| **Responsabilidad** | Lo que TODOS los componentes necesitan y que no es negocio: errores tipados, contratos de auditoría y opciones de configuración. |
| **NO incluye** | Nada que pertenezca a una pieza concreta. No sabe qué es un usuario ni qué es un préstamo. |
| **Interfaces** | `IPublicaAuditoria` |
| **Por qué existe** | Sin esto, cada pieza inventaría su propia forma de reportar un error y su propio acceso a configuración. |

Elementos:

| Elemento | Rol |
|---|---|
| `ExcepcionDominio` / `TipoError` | Rechazo controlado, con tipo y código estable. Es lo que permite responder 400/403/404/409/422 **sin filtrar trazas** (RD-08). |
| `EntradaAuditoria` / `IPublicaAuditoria` | Puerto de auditoría. Existe desde el día 1 aunque la persistencia llegue en la semana 14. |
| `SumideroDeAuditoria` | Implementación temporal: escribe en el log, no persiste. |
| `OpcionesCorreo` | Configuración tipada. Sin valores por defecto con secretos (RD-10). |

### 2.2 Control de acceso — `src/Biblioteca.Identidad` (pieza 1)

| | |
|---|---|
| **Responsabilidad** | Identidad de los usuarios: registro, activación, sesión, contraseñas, roles y la exigencia de rol de cada operación. |
| **NO incluye** | Envío de correo (encola, no envía), notificaciones, documentos, reportes. |
| **Depende de** | `Nucleo`. Y del **puerto** `IEncolaCorreo`, nunca de la implementación SMTP. |

Elementos:

| Elemento | Rol |
|---|---|
| `Rol` | `Estandar`, `Administrador`. Exactamente uno por usuario (RF-CA-04). |
| `Operaciones` | Claves estables de cada operación del sistema. |
| **`PoliticaDeOperaciones`** | **Punto único de exigencia de rol (RF-CA-05).** Aquí se lee qué roles puede ejecutar cada operación, y en ningún otro sitio. |
| `RequiereOperacionAttribute` | Declara la operación en la acción. No declara roles. |
| `CoherenciaDeOperaciones` | Regla de validación, sin dependencias de MVC, para poder probarla sola (RD-12). |
| `Usuario`, `Sesion`, `TokenActivacion`, `CodigoRecuperacion` | Entidades del Core más los atributos que exigen los criterios de aceptación. |
| `IdentidadDbContext` | Contexto propio. La pieza es dueña de sus tablas. |
| `IServicioDeSesiones` / `ServicioDeSesiones` | Crear, resolver, cerrar y revocar credenciales. |
| `PoliticaDeContrasenas` | RF-CA-14. Función pura. |
| `GeneradorDeSecretos` | Tokens de un solo uso (256 bits) y su hash SHA-256. |

### 2.3 Cola de correo — `src/Biblioteca.Correo`

| | |
|---|---|
| **Responsabilidad** | Encolar correos y entregarlos. |
| **NO incluye** | Lógica de negocio. No sabe quién es el usuario ni por qué se encoló el correo. |
| **Depende de** | `Nucleo`. **No depende de `Identidad`**, en ningún sentido: la dependencia va en el sentido contrario. |

| Elemento | Rol |
|---|---|
| `IEncolaCorreo` | **Puerto.** Lo que ve `Identidad`. |
| `EncolaCorreo` | Escribe la fila. No abre conexión SMTP. |
| `SmtpEntregador` | Reclama con `UPDATE` condicional y entrega. **El cliente SMTP se construye aquí**, de forma perezosa. |
| `CorreoEnCola`, `EstadoCorreo` | Entidad y sus estados: `Pendiente`, `Enviando`, `Enviado`, `Fallido`. |
| `CorreoDbContext` | Contexto propio. |

### 2.4 Módulo de negocio — `src/Biblioteca.Biblioteca`

| | |
|---|---|
| **Responsabilidad** | El negocio: préstamos, devoluciones y disponibilidad. |
| **NO incluye** | Nada del Core. No sabe qué es un permiso ni quién es un administrador. |
| **Depende de** | `Nucleo`. |

En la Práctica 1 sólo existe la **estructura** de la máquina de estados
(RF-NEG-03/04/05, RD-04). Ver [`docs/maquina-de-estados.md`](maquina-de-estados.md).

### 2.5 Host HTTP — `src/Biblioteca.Api`

| | |
|---|---|
| **Responsabilidad** | Traducir HTTP en llamadas a las piezas. Configuración,DI, migraciones y traducción de errores. |
| **NO incluye** | Reglas de negocio. Ni una sola. |
| **Depende de** | De las tres piezas. |

| Elemento | Rol |
|---|---|
| `Program.cs` | Cableado. Registra el proveedor SQL Server, que **no** está en las piezas. |
| `SaludController` | Comprobación de vida. `[AllowAnonymous]`, así que el guard de RF-CA-05 la exime. |
| `AutenticacionPorSesion` | Traduce el encabezado `Authorization` a una sesión válida. |
| `RequisitoDeOperacionHandler` | Aplica la política. **No decide nada**: consulta `PoliticaDeOperaciones`. |
| `ValidarOperacionesDeclaradasConvention` | Guard: si una acción no declara operación, la app no arranca. |
| `ManejadorDeExcepciones` | Traduce `ExcepcionDominio` a `ProblemDetails`. No inventa mensajes (RD-08). |
| `Migraciones/` | Las migraciones viven **aquí**, no en las piezas. |

### 2.6 Proceso emisor — `tools/Biblioteca.Correo.Enviador`

Ejecutable independiente que entrega los correos encolados (RF-NOT-09). Es un programa
 aparte, no un hilo de la API. Ver la justificación en la sección 4.3.

### 2.7 Pruebas

| Proyecto | Qué prueba |
|---|---|
| `Biblioteca.Nucleo.Tests` | Contratos del núcleo: tipos de error, entrada de auditoría. |
| `Biblioteca.Identidad.Tests` | Política de operaciones (RF-CA-05), guard de arranque, política de contraseñas (RF-CA-14), generación de tokens. |
| `Biblioteca.Biblioteca.Tests` | Estructura de la máquina de estados (RF-NEG-03/04/05). |
| `Biblioteca.Api.Tests` | Arranque real contra SQL Server, y barrido de que todo endpoint declara su operación. |

---

## 3. Reglas de dependencia

Las flechas van en un solo sentido. No hay ciclos, y hay pruebas que lo comprueban.

```
                  Biblioteca.Nucleo
                  (transversal)
                   ▲      ▲      ▲
                   │      │      │
        ┌──────────┘      │      └──────────┐
        │                 │                 │
Identidad ──(puerto)──▶ Correo     Biblioteca (negocio)
        ▲                 ▲                 ▲
        └────────┬────────┴─────────────────┘
                 │
            Biblioteca.Api   (host)
            Correo.Enviador  (proceso)

Biblioteca.Biblioteca ──▶ nadie lo importa (es el Core el que no depende del negocio,
                        no al revés: el negocio SÍ usa el Core más adelante)
```

Tres reglas que se defienden en la revisión:

| Regla | Consecuencia práctica |
|---|---|
| **El Core no conoce al módulo de negocio** (RD-03) | `Biblioteca.Biblioteca` no aparece en ningún archivo del Core. |
| **Correo no conoce Identidad** | La dependencia es `Identidad → IEncolaCorreo`, contra una interfaz. Por eso una prueba de recuperación de contraseña puede comprobar el correo encolado **sin SMTP**. |
| **El proveedor SQL Server vive en el host** | Las piezas dependen de EF Core y de su API relacional, no del proveedor. Por eso las migraciones viven en `Biblioteca.Api`. |

### Un `DbContext` por pieza

Cada pieza tiene su propio contexto y sus propias tablas. La alternativa —un contexto
único compartido— habría creado una dependencia entre piezas sólo por la persistencia,
que es exactamente lo que RD-01 prohíbe.

Consecuencia asumida y documentada: no hay transacción que abarque dos contextos. Al
cambiar una contraseña (Identidad) y encolar el aviso (Correo) son dos operaciones. Si el
encolado falla, la contraseña ya quedó cambiada. Se acepta: RF-NOT-08 exige que la
operación de negocio termine bien y el correo quede encolado, no que ambas cosas sean
atómicas.

---

## 4. Decisiones técnicas y su justificación

### 4.1 Sesiones en el servidor, no JWT — *para poder revocar*

**RF-CA-12, RF-CA-18 y RF-CA-20** exigen, los tres, revocar credenciales **ya
emitidas**.

Un JWT es autocontenido: lleva dentro su expiración y sus roles, y el servidor no tiene
dónde anotar que se invalidó. Para cumplir RF-CA-12 habría que esperar a que expirara.

Aquí el token es una cadena opaca de 256 bits. En la base sólo se guarda su **SHA-256**.
El servidor resuelve cada petición contra la tabla `Sesion` y puede cerrar las que quiera
con un `UPDATE`:

```csharp
// Un solo mecanismo sirve para los tres requisitos de revocación.
await db.Sesiones
    .Where(s => s.UsuarioId == usuarioId && s.CerradaEn == null)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CerradaEn, ahora));
```

Consecuencia: si alguien lee la base de datos, no encuentra ninguna credencial usable.
Es un beneficio, no un coste.

### 4.2 `TimeProvider` en vez de `DateTime.Now` — *para que los plazos se puedan probar*

**RF-CA-19** exige un bloqueo de 15 minutos. Probar eso esperando 15 minutos reales es
inaceptable, y RD-12 exige que los componentes se puedan probar sin la aplicación completa.

`TimeProvider` es la abstracción estándar de .NET para el reloj. Se registra una vez y se
inyecta en todas las piezas. En las pruebas se sustituye por un reloj simulado y el plazo
se comprueba al instante. Además resuelve RD-11 de paso: un único criterio de tiempo en
todo el sistema.

### 4.3 El emisor de correo es un programa aparte — *para que SMTP no bloquee nada*

**RF-NOT-08** exige que encolar un correo no abra conexión con el servidor de correo. La
prueba que se hace en la revisión es directa: *apagar el acceso al servidor SMTP y
registrar un usuario*.

Si el cliente SMTP se construjera al arrancar la aplicación, la API no levantaría sin
correo. Por eso se construye **dentro** del emisor, en el momento de entregar.

Y como además el envío no puede depender del proceso que encoló, el emisor es un
ejecutable independiente (`tools/Biblioteca.Correo.Enviador`), no un hilo ni un
`BackgroundService` de la API. Se ejecuta cuando se quiera, incluso después de que el
proceso que encoló ya terminó.

### 4.4 Reclamo condicional para que el envío no se duplique — *RF-NOT-12*

El requisito es explícito: *ejecutar dos veces el proceso de envío no duplica correos*.
La forma más sencilla de romperlo sería leer los pendientes y enviar, sin más.

Cada envío empieza con un `UPDATE` condicional:

```csharp
var tomado = await db.CorreosEnCola
    .Where(c => c.Id == id && c.Estado == EstadoCorreo.Pendiente)
    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCorreo.Enviando));

if (tomado == 0) continue;   // otro proceso ya lo reclamó: no se envía
```

Si otro proceso ya reclamó ese correo, la actualización afecta **0 filas** y aquí se
aborta. Con dos instancias corriendo a la vez, cada correo lo envía exactamente una.

### 4.5 Hash de contraseña con sal por usuario — *RF-CA-02*

`PasswordHasher<Usuario>` (PBKDF2-HMAC-SHA256, sal aleatoria por usuario), del propio
framework: cero dependencias y auditable.

El requisito pide dos cosas: que la contraseña no se guarde en claro y que **dos usuarios
con la misma contraseña no compartan el valor almacenado**. La sal aleatoria por usuario
da lo segundo de forma automática.

**No se usa ASP.NET Core Identity.** Trae su propio almacén de usuarios, sus propias
reglas de bloqueo y su propia verificación de contraseña, y chocan de frente con
RF-CA-19 (bloqueo de 5 intentos a 15 minutos), RF-CA-20 (desactivación) y RF-CA-22
(cambio de contraseña con la actual). Implementarlos aquí es el trabajo de la pieza 1.

### 4.6 Tokens de un solo uso hasheados en la base

Los tokens de activación (RF-CA-15) y los códigos de recuperación (RF-CA-10) se guardan
hasheados. Se envía el valor, se guarda el SHA-256.

Es coherente con RF-AUD-05, que prohíbe que un código de un solo uso entre en la
auditoría: si el valor no está en la base, tampoco puede filtrarse por un log mal escrito.

Se usa `FixedTimeEquals` para comparar, para que el tiempo de respuesta no diga si el
token existía.



### 4.8 Los tres mensajes de enumeración NO se unifican

Es una decisión deliberada y contraintuitiva:

| Situación | Respuesta | Por qué |
|---|---|---|
| **RF-CA-09 / RF-CA-17** — recuperación y reenvío | Siempre `202` con el mismo texto, exista o no el correo | Evitar que un atacante descubra qué correos están registrados |
| **RF-CA-15** — inicio de sesión con cuenta no activada | Error **explícito**: «la cuenta no está activa» | El requisito lo pide. El usuario acaba de registrarse y necesita saber qué hacer |

Unificar estos dos casos parecería más seguro, pero **rompería RF-CA-15**, que exige el
mensaje específico. Se mantienen distintos a propósito y cada uno cita su requisito.

### 4.9 Ninguna regla de negocio se resuelve con una excepción no controlada

Un rechazo de negocio **nunca** es una excepción no controlada. Cada rechazo esperado es
una `ExcepcionDominio` con tipo y código, y el middleware la traduce a `ProblemDetails`.
El `catch (Exception)` que existe en `SmtpEntregador` sólo deja constancia del fallo de
un envío, y no se traga nada: registra y propaga.

---

## 5. Qué NO incluye cada componente

La revisión penaliza el exceso tanto como la omisión.

| Componente | NO incluye |
|---|---|
| `Nucleo` | Nada de negocio. No conoce usuarios, correos ni préstamos. |
| `Identidad` | El envío de correo. Encola a través de un puerto, no envía. Tampoco la persistencia de auditoría. |
| `Correo` | Nada de identidad. No sabe quién encoló ni por qué. No implementa plantillas de correo. |
| `Biblioteca` | Nada del Core. En la Práctica 1, tampoco la lógica de los préstamos: sólo la estructura de estados. |
| `Api` | **Ninguna regla de negocio.** Si aparece un `if` de negocio en un controlador, es un error de diseño. |
| `Correo.Enviador` | No lee nada de `Identidad`. No aplica plantillas: entrega el cuerpo que ya está en la fila. |

---

## 6. Requisitos NO calificados en la Práctica 1

Para que quede constancia de que no es una omisión:

| Requisito | Cuándo se califica |
|---|---|
| RF-CA-08 — registro de auditoría del cambio de rol | Semana 14, pieza 6 |
| RF-CA-13 — registro de auditoría del restablecimiento forzado | Semana 14, pieza 6 |
| RF-CA-20 — registro de auditoría de la desactivación | Semana 14, pieza 6 |
| RF-NEG-03/04/05 — prueba completa de la máquina de estados | Semana 8 (aquí sólo la estructura) |
| RF-REP-* — reportes | Semana 12 |
| RF-DOC-* — documentos | Semana 9 |
| RF-AUD-* — persistencia de auditoría | Semana 14 |

El puerto `IPublicaAuditoria` existe desde el día 1 precisamente para que la semana 14
sea sustituir una implementación, no reescribir las piezas.

---

## 7. Revisión acumulada desde la Práctica 2

A partir de la Práctica 2, la revisión vuelve a comprobar que lo de la Práctica 1 sigue
funcionando:

- registro con activación por correo;
- inicio de sesión;
- rechazo por rol insuficiente;
- recuperación de contraseña.

Por eso el commit base incluye pruebas de la política de operaciones, de la política de
contraseñas y del guard de arranque: son la red de seguridad de esa revisión acumulada.