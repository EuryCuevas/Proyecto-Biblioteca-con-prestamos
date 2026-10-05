# Biblioteca — Core + módulo de negocio

Proyecto final del curso. Contiene dos partes:

- **El Core**, compartido e idéntico para los 25 estudiantes del grupo.
- **El módulo de negocio**, en este caso **Biblioteca**: catálogo de recursos, préstamos,
  devoluciones y disponibilidad.

**Práctica 1 — Control de acceso (Bloque 2, Semana 4).**

---

## Índice

1. [Requisitos previos](#1-requisitos-previstos)
2. [Puesta en marcha](#2-puesta-en-marcha)
3. [Variables de entorno](#3-variables-de-entorno)
4. [Estructura del repositorio](#4-estructura-del-repositorio)
5. [Comandos habituales](#5-comandos-habituales)
6. [Cómo se comprueba cada criterio de aceptación](#6-cómo-se-comprueba-cada-criterio-de-aceptación)
7. [Dónde está cada cosa](#7-dónde-está-cada-cosa)
8. [Qué NO incluye esta práctica](#8-qué-no-incluye-esta-práctica)

---

## 1. Requisitos previos

| Herramienta | Versión | Para qué |
|---|---|---|
| **.NET SDK** | 10.0.302 o superior | Compilar y ejecutar |
| **SQL Server** | 2022 o superior | Base de datos. Se desarrolla contra la instancia local. |
| **SQL Server Management Studio** | 20 o superior | Administrar la base a mano |
| **dotnet-ef** | 10.0.x | Sólo para crear migraciones nuevas |

Comprobar que todo está en su sitio:

```powershell
dotnet --version
dotnet tool install --global dotnet-ef --version 10.*
```

> La herramienta `dotnet-ef` es global. Si `dotnet ef` no se reconoce, cierra y abre la
> terminal otra vez.

---

## 2. Puesta en marcha

### Paso 1 — Definir la cadena de conexión

La aplicación **no arranca** sin la variable `ConnectionStrings__Biblioteca`. No hay valor
por defecto a propósito: si lo hubiera, bastaría con cambiar una línea para apuntar a otra
base sin darse cuenta (RD-10).

**En PowerShell, para la sesión actual:**

```powershell
$env:ConnectionStrings__Biblioteca = "Server=localhost;Database=Biblioteca;Trusted_Connection=True;TrustServerCertificate=True"
```

**Para que sea permanente** (sólo lo necesario si quieres que sobreviva al cierre de la
terminal):

```powershell
[Environment]::SetEnvironmentVariable(
    "ConnectionStrings__Biblioteca",
    "Server=localhost;Database=Biblioteca;Trusted_Connection=True;TrustServerCertificate=True",
    "User")
```

| Parte | Significado |
|---|---|
| `Server=localhost` | Instancia local. Cámbiala si usas una instancia con nombre: `Server=localhost\EURY`. |
| `Database=Biblioteca` | Nombre de la base. |
| `Trusted_Connection=True` | Autenticación de Windows: **no hay contraseña que escribir ni que versionar**. |

### Paso 2 — Crear la base de datos

Abre **SSMS** y conéctate a la instancia local. Ejecuta:

```sql
IF DB_ID('Biblioteca') IS NULL
BEGIN
    CREATE DATABASE Biblioteca;
END
```

> Si prefieres la línea de comandos:
> ```powershell
> sqlcmd -S localhost -E -Q "IF DB_ID('Biblioteca') IS NULL CREATE DATABASE Biblioteca"
> ```

### Paso 3 — Crear las tablas

**Las migraciones se aplican solas al arrancar la API.** No hace falta ejecutar nada a
mano: `Program.cs` aplica las migraciones de las tres piezas en cada arranque.

Si prefieres aplicarlas desde la consola, o si la API no está corriendo:

```powershell
dotnet ef database update --project src/Biblioteca.Api --context IdentidadDbContext
dotnet ef database update --project src/Biblioteca.Api --context CorreoDbContext
dotnet ef database update --project src/Biblioteca.Api --context BibliotecaDbContext
```

### Paso 4 — Arrancar

```powershell
dotnet run --project src/Biblioteca.Api
```

Salida esperada:

```
Now listening on: http://localhost:5XXX
```

### Paso 5 — Comprobar que funciona

```powershell
Invoke-RestMethod http://localhost:5XXX/salud
```

Debe devolver el estado del servicio. Si devuelve `ok`, la aplicación leyó la
configuración, abrió la conexión, aplicó las migraciones y responde.

---

## 3. Variables de entorno

**Ningún valor secreto está en el repositorio** (RD-10, RF-NOT-13). `appsettings.json`
sólo contiene valores por defecto sin contradicción y **claves con el nombre de la
variable de entorno equivalente**, para que se sepa qué falta.

Los nombres y su propósito están también en [`.env.example`](.env.example).

| Variable | Obligatoria | Propósito |
|---|:---:|---|
| `ConnectionStrings__Biblioteca` | **Sí** | Cadena de conexión a SQL Server. Sin ella la API no arranca. |
| `Correo__Host` | No | Servidor SMTP de salida. **Vacío = el registro funciona igual**, porque el correo sólo se encola (RF-NOT-08). |
| `Correo__Puerto` | No | Puerto del servidor SMTP. Por defecto `587`. |
| `Correo__Usuario` | No | Usuario de autenticación en el servidor SMTP. |
| `Correo__Contrasena` | No | Contraseña o **contraseña de aplicación** del correo. Nunca se versiona. |
| `Correo__UsarSsl` | No | `true` para STARTTLS. Por defecto `true`. |
| `Correo__Remitente` | No | Dirección desde la que se envía. Sin ella el emisor avisa que falta y termina con código 1. |

### Cómo comprobar que una variable está definida

```powershell
$env:ConnectionStrings__Biblioteca -ne $null
$env:Correo__Host -ne $null
```

### Datos que SÍ están en el repositorio

Estos valores **no son secretos** y viven en `appsettings.json`:

| Clave | Valor | Por qué no es secreto |
|---|---|---|
| `Correo:Puerto` | `587` | Puerto estándar. |
| `Correo:UsarSsl` | `true` | Valor seguro por defecto. |
| `Correo:MinutosValidezToken` | `30` | Decisión de negocio. |
| `Correo:MinutosBloqueo` | `15` | Decisión de negocio (RF-CA-19). |
| `Correo:IntentosMaximos` | `5` | Decisión de negocio (RF-CA-19). |
| `Correo:HorasValidezSesion` | `8` | Decisión de negocio. |

---

## 4. Estructura del repositorio

```
Biblioteca.sln

src/
  Biblioteca.Nucleo/          Transversal: errores tipados, puertos (auditoría, correo), configuración
  Biblioteca.Identidad/       PIEZA 1 del Core. Usuario, sesión, acceso, roles, registro, contraseñas
  Biblioteca.Correo/          Cola de correo y entrega SMTP
  Biblioteca.Biblioteca/      MÓDULO DE NEGOCIO. Catálogo, socios, préstamos y sus relaciones
  Biblioteca.Api/             Host HTTP. Configuración, DI, migraciones, traducción de errores

tools/
  Biblioteca.Correo.Enviador/ Proceso independiente que envía los correos encolados

tests/
  Biblioteca.Nucleo.Tests/      Contratos del núcleo transversal
  Biblioteca.Identidad.Tests/   Política de operaciones, guard de arranque, contraseñas, tokens
  Biblioteca.Biblioteca.Tests/  Máquina de estados del negocio y relaciones del modelo de datos
  Biblioteca.Api.Tests/         Arranque real contra SQL Server y flujos de extremo a extremo

docs/
  diseno-de-componentes.md    Documento de diseño de componentes (semana 2)
  maquina-de-estados.md       Estados y transiciones del módulo de negocio
```

### Regla de dependencia que se puede comprobar

El **Core no depende del módulo de negocio** (RD-03). Ningún archivo del Core importa
`Biblioteca.Biblioteca`. Para comprobarlo:

```powershell
Select-String -Path "src\Biblioteca.Nucleo\**\*.cs","src\Biblioteca.Identidad\**\*.cs","src\Biblioteca.Correo\**\*.cs" -Pattern "Biblioteca\.Biblioteca"
```

Sin resultados.

**Ninguna pieza referencia a otra pieza.** Se comprueba en los dos sentidos:

```powershell
Select-String -Path "src\Biblioteca.Identidad\**\*.cs" -Pattern "Biblioteca\.Correo"
Select-String -Path "src\Biblioteca.Correo\**\*.cs" -Pattern "Biblioteca\.Identidad"
Select-String -Path "src\Biblioteca.Correo\**\*.cs","src\Biblioteca.Identidad\**\*.cs" -Pattern "Biblioteca\.Biblioteca"
```

Sin resultados en ninguno de los tres.

Para que `Identidad` pueda encolar un correo sin referenciar la pieza que lo entrega, el
**puerto vive en el Core**: `Biblioteca.Nucleo/Notificacion/IEncolaCorreo.cs`. La pieza
`Correo` lo implementa (`EncolaCorreo`); `Identidad` sólo lo consume. Es el mismo patrón
que el puerto de auditoría `IPublicaAuditoria`. Referenciar `Biblioteca.Correo` desde
`Biblioteca.Identidad` habría acoplado la pieza 1 al detalle de SMTP.

---

## 5. Comandos habituales

| Qué quiero | Comando |
|---|---|
| Compilar todo | `dotnet build Biblioteca.sln` |
| Ejecutar todas las pruebas | `dotnet test Biblioteca.sln` |
| Ejecutar sólo las de Identidad | `dotnet test tests/Biblioteca.Identidad.Tests` |
| Ejecutar sólo las de la máquina de estados | `dotnet test tests/Biblioteca.Biblioteca.Tests` |
| Arrancar la API | `dotnet run --project src/Biblioteca.Api` |
| Enviar los correos pendientes | `dotnet run --project tools/Biblioteca.Correo.Enviador` |
| Crear una migración nueva | Ver más abajo |
| Ver el esquema en SSMS | Abrir `localhost` → base `Biblioteca` |

### Crear una migración nueva

Las migraciones viven en el host, no en las piezas (para que las piezas no dependan del
proveedor SQL Server):

```powershell
dotnet ef migrations add NombreDeLaMigracion `
  --project src/Biblioteca.Api `
  --startup-project src/Biblioteca.Api `
  --context IdentidadDbContext `
  --output-dir Migraciones/Identidad
```

Los tres contextos disponibles son `IdentidadDbContext`, `CorreoDbContext` y
`BibliotecaDbContext`, con sus carpetas `Migraciones/Identidad`, `Migraciones/Correo` y
`Migraciones/Biblioteca`.

---

## 6. Cómo se comprueba cada criterio de aceptación

> **Regla del curso:** lo que el README no dice, no se busca. Esta sección existe para que
> no haya que adivinar.

### 6.1 La aplicación arranca y responde

```powershell
dotnet run --project src/Biblioteca.Api
Invoke-RestMethod http://localhost:5XXX/salud
```

Devuelve el estado del servicio. Prueba de paso 5.

### 6.2 La base de datos se crea y se puebla

Abrir **SSMS** → base `Biblioteca`. Deben existir estas diez tablas:

| Tabla | Pertenece a | Qué contiene |
|---|---|---|
| `Usuario` | Core · Identidad | Nombre, correo, hash de contraseña, rol, activo, intentos fallidos, bloqueado hasta |
| `Sesion` | Core · Identidad | Token **hasheado**, creada, expira, cerrada, motivo de cierre |
| `TokenActivacion` | Core · Identidad | Token hasheado, emitido, vence, usado |
| `CodigoRecuperacion` | Core · Identidad | Código hasheado, emitido, vence, usado |
| `CorreoEnCola` | Core · Correo | Destinatario, asunto, cuerpo, estado, intentos, creado, enviado, último error |
| `Recurso` | Módulo de negocio | Título, autor, ISBN, editorial, año, género, activo |
| `Ejemplar` | Módulo de negocio | Copia física: código, recurso al que pertenece, ingreso, retirado |
| `Socio` | Módulo de negocio | Número de socio, usuario del Core, alta, activo, cupos |
| `Prestamo` | Módulo de negocio | Socio, ejemplar, estado, fechas |
| `__EFMigrationsHistory` | EF Core | Migraciones aplicadas |

Comprobación en SSMS:

```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
```

**Las tres relaciones existen como llaves foráneas de verdad** (RF-NEG-01 pide que estén
en el modelo de datos, no sólo en el diagrama):

```sql
SELECT OBJECT_NAME(parent_object_id) + '.' + name AS Llave,
       OBJECT_NAME(referenced_object_id)            AS ApuntaA
FROM sys.foreign_keys ORDER BY Llave;
```

| Llave | Apunta a | Relación |
|---|---|---|
| `FK_Ejemplar_Recurso_RecursoId` | `Recurso` | un recurso tiene N ejemplares |
| `FK_Prestamo_Socio_SocioId` | `Socio` | un socio tiene N préstamos |
| `FK_Prestamo_Ejemplar_EjemplarId` | `Ejemplar` | un ejemplar se presta N veces |

Las únicas llaves foráneas que apuntan a `Usuario` son `Sesion`, `TokenActivacion` y
`CodigoRecuperacion`: **las tres son del Core**. Ninguna tabla del módulo de negocio
apunta al Core (RD-03). Para comprobarlo:

```sql
SELECT name FROM sys.indexes WHERE is_unique = 1 AND name = 'UQ_Socio_UsuarioId';
```

Devuelve una fila. El índice es **único y sin llave foránea**: un usuario del Core es como
mucho un socio, y la unicidad la impone la base sin atar este esquema al del Core.

### 6.3 El estado se guarda como texto legible

```sql
SELECT Estado, COUNT(*) AS Total FROM Prestamo GROUP BY Estado;
```

`Estado` es `NVARCHAR(20)` con restricción `CHECK`, no un entero: el valor se lee en
SSMS sin traducir (RF-REP-03 lo necesita).

### 6.4 La sesión se guarda **hasheada**, no en claro

```sql
SELECT Id, CreadaEn, ExpiraEn, CerradaEn,
       CONVERT(VARCHAR(64), TokenHash, 2) AS TokenHashHex
FROM Sesion;
```

`TokenHash` es `BINARY(32)`: 32 bytes de SHA-256. **No hay ningún token utilizable en la
base.** La misma comprobación vale para `TokenActivacion.TokenHash` y
`CodigoRecuperacion.CodigoHash`.

### 6.5 No se puede dar de alta dos veces el mismo correo

```sql
SELECT name FROM sys.indexes
WHERE is_unique = 1 AND name LIKE 'UQ_%';
```

Deben aparecer `UQ_Usuario_Correo`, `UQ_Sesion_TokenHash`, `UQ_TokenActivacion_TokenHash` y
`UQ_CodigoRecuperacion_CodigoHash`. La unicidad la impone la base, no sólo el código
(RF-CA-01).

### 6.6 Un ejemplar no puede tener dos préstamos vivos

```sql
SELECT name, filter_definition FROM sys.indexes WHERE name = 'UX_Prestamo_EjemplarVivo';
```

El índice es único y filtrado a los estados `Solicitado` y `Activo`. Es la base de la
disponibilidad: un mismo ejemplar no puede estar prestado dos veces a la vez.

Por eso **`Ejemplar` no tiene columna «Disponible»**: la disponibilidad se deriva de los
préstamos vivos. Una bandera mantenida a mano son dos verdades que se desincronizan
en cuanto una actualización falla a mitad; derivada, hay una sola.

```powershell
dotnet test tests/Biblioteca.Biblioteca.Tests --filter "FullyQualifiedName~RelacionesDelModelo"
```

Las 13 pruebas de `RelacionesDelModeloTests` verifican las tres relaciones, que
`Socio.UsuarioId` es único sin llave foránea, que `Ejemplar` no tiene columna de
disponibilidad y que `UX_Prestamo_EjemplarVivo` sigue siendo único y filtrado.

### 6.7 El estado del préstamo sólo puede ser uno de los cinco

```sql
SELECT name, definition FROM sys.check_constraints;
```

Debe existir `CK_Prestamo_Estado` con los cinco valores. La restricción vive en la base,
así que ningún camino puede dejar un estado fuera de la máquina.

### 6.8 Las fechas se guardan en UTC (RD-11)

```sql
SELECT name, system_type_name, scale FROM sys.columns
WHERE name LIKE '%En' OR name LIKE 'Vence%';
```

Todas `datetime2`. Los valores se escriben en UTC y se convierten al presentarlos.

### 6.9 No hay secretos en el repositorio

**Comprobación 1 — la cadena de conexión no está en ningún archivo versionado:**

```powershell
git grep -n "Server=" -- src
```

Sin resultados. La cadena de conexión sólo existe en la variable de entorno
`ConnectionStrings__Biblioteca`.

**Comprobación 2 — `appsettings.json` sólo contiene claves, no valores:**

```powershell
Get-Content src/Biblioteca.Api/appsettings.json | Select-String "Server="
```

Sin resultados. Cada clave sensible aparece como comentario con el nombre de su variable
de entorno, nunca con su valor.

**Comprobación 3 — los archivos de secretos no están versionados:**

```powershell
git ls-files | Select-String -Pattern "\.env$|appsettings\.Local\.json|\.pfx$|\.key$"
```

Sin resultados: esos patrones están en `.gitignore` y nunca se versionan (RD-10).

**Comprobación 4 — `.env.example` sólo documenta nombres:**

```powershell
Get-Content .env.example
```

Los valores de `Correo__Usuario`, `Correo__Contrasena` y `Correo__Remitente` aparecen
**vacíos**. El de `ConnectionStrings__Biblioteca` aparece con la configuración de
desarrollo, que no contiene contraseña porque usa autenticación de Windows.

### 6.10 La exigencia de rol de cada operación está en un solo punto (RF-CA-05)

**Este es el punto que más se revisa.** Está en:

```
src/Biblioteca.Identidad/PoliticaDeOperaciones.cs
```

Ahí se lee, en una sola tabla, qué roles puede ejecutar cada operación. Ningún otro
archivo decide eso. Para verlo:

```powershell
Select-String -Path src\Biblioteca.Identidad\PoliticaDeOperaciones.cs -Pattern "Operaciones\."
```

Y para comprobar que **ningún endpoint puede saltárselo** (una acción sin operación
declarada impide que la aplicación arranque):

```powershell
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~GuardDeOperaciones"
```

Declarar la operación es sólo la mitad; la otra mitad es **que alguien la evalúe**. El hilo
es este, y cada peldaño tiene su sitio:

```
[RequiereOperacion("usuarios.forzarRestablecimiento")]        ← la acción nombra la operación
        ↓
Program.cs, política de repliego: .AddRequirements(new RequisitoDeOperacion())
        ↓
RequisitoDeOperacionHandler: lee la operación de los metadatos de la acción
        ↓
PoliticaDeOperaciones.RolesDe(operación)                     ← quién puede, en un solo punto
```

El requisito va en la **política de repliego** y no en cada acción, así que el handler se
ejecuta en todas las que no son `[AllowAnonymous]` sin que ningún controlador tenga que
acordarse de pedirlo. Para verlo:

```powershell
Select-String -Path src\Biblioteca.Api\Program.cs -Pattern "AddRequirements"
```

Y el extremo que de verdad importa —un Estándar contra un endpoint de Administrador, con la
petición escrita a mano— está en `RecuperacionDeContrasenaTests` (§6.14) y, con las cuatro
operaciones de administración barreadas de una vez, en `AdministracionDeUsuariosTests` (§6.20):

```powershell
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~Un_estandar_recibe_403"
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~Un_estandar_recibe_403_en_cada"
```

> Esa prueba existe porque este requisito fallaba. `RequisitoDeOperacionHandler` estaba
> registrado en el contenedor y **no lo invocaba nadie**: ASP.NET Core sólo evalúa un handler
> si alguna política le pide su requisito, y ninguna lo pedía. La política de repliego sólo
> exigía estar autenticado, de modo que la exigencia de rol no se comprobaba en ninguna
> petición y cualquier credencial válida llegaba a cualquier endpoint. El guard de arranque,
> la tabla de la política y el barrido de pruebas eran correctos; faltaba el cable entre
> ellos. Lo destapó `POST /usuarios/restablecer-contrasena`, la primera llamada
> de extremo a extremo que hace un Estándar sobre un endpoint reservado al Administrador.

### 6.11 Registrar y activar una cuenta, de extremo a extremo (RF-CA-01, 02, 14, 15, 16, 17)

Los seis criterios se comprueba con la misma secuencia, que es exactamente lo que hace el
enunciado: registrar con un correo propio, abrir el enlace recibido y comprobar que la
cuenta pasa a estar activa.

```powershell
$base   = "http://localhost:5XXX"
$correo = "yo@ejemplo.com"
$cuerpo = @{ nombre = "Yo Mismo"; correo = $correo; contrasena = "Biblioteca2026" } | ConvertTo-Json

# 1. Registrar la cuenta.
Invoke-RestMethod "$base/usuarios/registro" -Method Post -ContentType "application/json" -Body $cuerpo
# Respuesta: 202  {"mensaje":"Cuenta registrada. Revisa tu correo para activarla."}
```

```sql
-- La cuenta existe pero está INACTIVA (RF-CA-15) y la contraseña NO está en claro (RF-CA-02).
SELECT Correo, Activo, PasswordHash
FROM Usuario WHERE Correo = 'yo@ejemplo.com';
-- Activo = 0. PasswordHash no contiene 'Biblioteca2026'.
-- Dos usuarios con la misma contraseña tienen PasswordHash DISTINTO: eso es la sal por usuario.

-- El correo quedó encolado, con el enlace (RF-NOT-08).
SELECT Destinatario, Estado, Cuerpo
FROM CorreoEnCola WHERE Destinatario = 'yo@ejemplo.com';
-- Estado = Pendiente. En Cuerpo aparece: http://localhost:5XXX/usuarios/activar?token=...
```

```powershell
# 2. Abrir el enlace: copiar el token del paso anterior (RF-CA-16).
Invoke-RestMethod "$base/usuarios/activar?token=<TOKEN>"
# Respuesta: 200  {"mensaje":"Cuenta activada. Ya puedes iniciar sesión."}
# SELECT Activo FROM Usuario WHERE Correo = 'yo@ejemplo.com';  ->  1

# 3. El enlace NO sirve una segunda vez, y el estado NO cambia (RF-CA-16).
Invoke-RestMethod "$base/usuarios/activar?token=<TOKEN>"
# Respuesta: 422  {"title":"El enlace ya se usó o ha vencido. Pide uno nuevo."}
# Activo sigue en 1: un rechazo no desactiva lo que ya estaba activo.

# 4. Un token inventado se rechaza igual (404) que uno caducado: no se puede
#    deducir qué enlaces existieron (RF-CA-16).
Invoke-RestMethod "$base/usuarios/activar?token=inventado"
```

```powershell
# 5. El correo es único (RF-CA-01). Segundo registro con el mismo correo -> 409.
Invoke-RestMethod "$base/usuarios/registro" -Method Post -ContentType "application/json" -Body $cuerpo

# El mismo correo con MAYÚSCULAS también es el mismo correo -> 409:
Invoke-RestMethod "$base/usuarios/registro" -Method Post -ContentType "application/json" `
  -Body (@{ nombre = "Otro"; correo = "YO@EJEMPLO.COM"; contrasena = "Biblioteca2026" } | ConvertTo-Json)

# 6. La política de contraseña (RF-CA-14) se rechaza con 400 y su motivo, y NO deja
#    usuario a medias. Las tres formas de incumplirla:
#    "solosletras"  -> 400 "La contraseña debe combinar letras y números."
#    "12345678"     -> 400 "La contraseña debe combinar letras y números."
#    "corta1"       -> 400 "La contraseña debe tener al menos 8 caracteres."
Invoke-RestMethod "$base/usuarios/registro" -Method Post -ContentType "application/json" `
  -Body (@{ nombre = "Yo"; correo = "otro@ejemplo.com"; contrasena = "solosletras" } | ConvertTo-Json)

# 7. Un correo mal formado se rechaza con 400, no con un error de servidor (RD-07).
Invoke-RestMethod "$base/usuarios/registro" -Method Post -ContentType "application/json" `
  -Body (@{ nombre = "Yo"; correo = "no-es-un-correo"; contrasena = "Biblioteca2026" } | ConvertTo-Json)
```

```powershell
# 8. El reenvío NO revela qué correos están registrados (RF-CA-17).
#    Registra primero otro usuario sin activar para tener los dos casos.
$reenvio = { param($c) Invoke-RestMethod "$base/usuarios/reenviar-activacion" `
               -Method Post -ContentType "application/json" -Body (@{ correo = $c } | ConvertTo-Json) }

& $reenvio "yo@ejemplo.com"          # -> 202, cuerpo: "Si el correo está registrado y la cuenta
& $reenvio "nadie@ejemplo.com"       # -> 202, cuerpo: INACCIÓN, y la cuenta queda inactiva...
```

Los dos `202` devuelven **el mismo cuerpo, byte a byte**, y en ambos casos se encola un
correo nuevo. Además, el reenvío **invalida el enlace anterior**: el token viejo pasa a
`UsadoEn` y ya no activa nada (→ `422`).

```powershell
# 9. Todo lo anterior, automáticamente:
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~RegistroYActivacion"
# 19 pruebas. No simulan nada: hablan HTTP contra la aplicación real y comprueban lo que
# queda en Usuario, en TokenActivacion y en CorreoEnCola.
```

### 6.12 La máquina de estados del negocio es correcta (RF-NEG-03/04/05)

```powershell
dotnet test tests/Biblioteca.Biblioteca.Tests
```

Las 19 pruebas verifican el número de estados, los terminales, las transiciones
prohibidas y que ninguna transición esté a la vez permitida y prohibida.

La tabla completa, con el diagrama, está en [`docs/maquina-de-estados.md`](docs/maquina-de-estados.md).

### 6.13 Iniciar sesión, consultar la identidad y cerrar sesión (RF-CA-03, 07, 18, 19)

Tres endpoints, y son los tres que exige el enunciado para esta funcionalidad:

| Método | Ruta | ¿Exige sesión? | Criterio |
|---|---|---|---|
| `POST` | `/sesion/iniciar` | No | RF-CA-03, RF-CA-15, RF-CA-19 |
| `GET` | `/sesion/yo` | Sí | RF-CA-07 |
| `POST` | `/sesion/cerrar` | Sí | RF-CA-18 |

Arranca la API y ten a mano un usuario **activado** (la sección «Registrar y activar una
cuenta, de extremo a extremo» explica cómo conseguirlo). En lo que sigue, `$clave` es la
contraseña con la que lo activaste.

#### a) Credenciales correctas abren sesión (RF-CA-03)

```powershell
$api = "http://localhost:5XXX"
$sesion = Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = $clave } | ConvertTo-Json)

$sesion.token        # credencial de sesión, en claro
$sesion.usuario.rol  # "Estandar"
```

La credencial también viene en la cabecera `Authorization: Bearer <token>` de la misma
respuesta. **En la base sólo queda su SHA-256**:

```sql
SELECT CONVERT(VARCHAR(64), TokenHash, 2) AS TokenHashHex, CerradaEn FROM Sesion;
```

`TokenHash` es `BINARY(32)` y el valor en claro no aparece en ninguna fila.

#### b) Credenciales incorrectas se rechazan **sin revelar cuál de los dos datos falló** (RF-CA-03)

Ésta es la comprobación literal que pide el enunciado. Los dos rechazos tienen que ser
**idénticos byte a byte**:

```powershell
$malClave = Invoke-WebRequest "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = "ClaveEquivocada9" } | ConvertTo-Json) `
    -SkipHttpErrorCheck

$sinCorreo = Invoke-WebRequest "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "nadie@example.com"; contrasena = $clave } | ConvertTo-Json) `
    -SkipHttpErrorCheck

$malClave.StatusCode                      # 401
$malClave.Content -eq $sinCorreo.Content   # True
```

El mensaje es siempre `Correo o contraseña incorrectos.` Un correo **mal formado** tampoco
produce un error distinto: se rechaza con ese mismo mensaje.

> **Por qué 401 y no 403.** 403 significa «sé quién eres pero no puedes», que es lo que
> corresponde a un rol insuficiente. Una contraseña mal escrita es «no sé quién eres».
> Por eso el núcleo distingue `TipoError.NoAutenticado` (401, con su cabecera
> `WWW-Authenticate`) de `TipoError.NoAutorizado` (403).

#### c) Consulta del usuario autenticado y su rol (RF-CA-07)

```powershell
Invoke-RestMethod "$api/sesion/yo" -Headers @{ Authorization = "Bearer $($sesion.token)" }
```

Devuelve `id`, `nombre`, `correo`, `rol` y `activo`. Sin sesión válida **se rechaza**:

```powershell
Invoke-WebRequest "$api/sesion/yo" -SkipHttpErrorCheck | Select-Object StatusCode  # 401
```

#### d) Cierre de sesión: la credencial cerrada deja de servir (RF-CA-18)

```powershell
$cabecera = @{ Authorization = "Bearer $($sesion.token)" }

Invoke-WebRequest "$api/sesion/cerrar" -Method Post -Headers $cabecera -SkipHttpErrorCheck |
    Select-Object StatusCode                                                      # 204

# La MISMA credencial, usada otra vez:
Invoke-WebRequest "$api/sesion/yo" -Headers $cabecera -SkipHttpErrorCheck |
    Select-Object StatusCode                                                      # 401
```

Y en la base, la fila queda marcada como cerrada (esto es lo que invalida la credencial):

```sql
SELECT CerradaEn, MotivoCierre FROM Sesion ORDER BY CreadaEn DESC;
```

#### e) Bloqueo tras cinco intentos fallidos consecutivos (RF-CA-19)

```powershell
1..5 | ForEach-Object {
    Invoke-WebRequest "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
        -Body (@{ correo = "ana@example.com"; contrasena = "ClaveEquivocada9" } | ConvertTo-Json) `
        -SkipHttpErrorCheck | Out-Null
}

# El sexto intento, con la contraseña CORRECTA, también se rechaza:
Invoke-WebRequest "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = $clave } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode                                  # 401
```

Los cinco intentos fallidos responden `Correo o contraseña incorrectos.`; a partir del
sexto, el mensaje pasa a ser `Cuenta bloqueada por intentos fallidos. Inténtalo de nuevo
a las HH:mm (UTC).` El estado queda así:

```sql
SELECT Correo, Activo, IntentosFallidos, BloqueadoHasta FROM Usuario;
```

| `IntentosFallidos` | `BloqueadoHasta` | Qué significa |
|---|---|---|
| 0 | `NULL` | Sin historial. Un inicio de sesión correcto lo deja aquí. |
| 1 a 4 | `NULL` | Todavía quedan intentos. |
| 5 | una fecha | Bloqueada 15 minutos (`OpcionesCorreo:MinutosBloqueo`). |

Cuando el bloqueo **vence**, el contador vuelve a cero: si no, un solo fallo posterior
volvería a bloquear la cuenta y el usuario nunca podría entrar. Para verlo sin esperar
15 minutos:

```sql
UPDATE Usuario SET BloqueadoHasta = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE Correo = 'ana@example.com';
```

Con el bloqueo ya vencido, la contraseña correcta entra y el contador queda a cero:

```powershell
Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = $clave } | ConvertTo-Json) |
    Select-Object -ExpandProperty usuario
```

Y un fallo posterior **no** vuelve a bloquear la cuenta: quedan cinco intentos nuevos.

> **Una tensión que conviene conocer.** RF-CA-03 pide no revelar cuál de los dos datos
> falló, y RF-CA-19 pide decir que la cuenta está bloqueada. Son contradictorias: el
> mensaje de bloqueo sólo existe si la cuenta existe. Aquí se cumplen las dos, y se
> acepta que el bloqueo revele la existencia de la cuenta — es lo que el enunciado pide
> literalmente. Donde sí se cierra la enumeración es en RF-CA-15: el mensaje «la cuenta
> no está activa» **sólo** aparece si la contraseña es correcta.

```powershell
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~SesionTests"
```

Las 21 pruebas cubren los cinco puntos de arriba de extremo a extremo: no se simula nada,
se habla HTTP contra la aplicación real y se comprueba la base.

### 6.14 Recuperar la contraseña, restablecerla y cambiarla (RF-CA-09, 10, 11, 12, 13, 22)

Seis criterios, cinco endpoints:

| Método | Ruta | ¿Exige sesión? | Criterio |
|---|---|---|---|
| `POST` | `/contrasenas/recuperacion` | No | RF-CA-09, RF-CA-10 |
| `GET` | `/contrasenas/recuperacion?codigo=…` | No | RF-CA-10 |
| `POST` | `/contrasenas/restablecer` | No | RF-CA-10, RF-CA-11, RF-CA-12 |
| `POST` | `/contrasenas/propia` | Sí | RF-CA-22 (+ RF-CA-14, RF-CA-12) |
| `POST` | `/usuarios/restablecer-contrasena` | Sí, sólo Administrador | RF-CA-13 |

Arranca la API y ten a mano un usuario **activado** (§6.11 lo explica). En lo que sigue
`$clave` es su contraseña, y `$usuarioId` su identificador:

```powershell
$api = "http://localhost:5XXX"
$usuarioId = (Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = $clave } | ConvertTo-Json)).usuario.id
```

#### a) Pedir el código no revela qué correos están registrados (RF-CA-09)

Ésta es la comprobación literal que pide el enunciado. Las tres respuestas tienen que ser
**idénticas byte a byte**:

```powershell
$existe = Invoke-WebRequest "$api/contrasenas/recuperacion" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com" } | ConvertTo-Json)

$noExiste = Invoke-WebRequest "$api/contrasenas/recuperacion" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "nadie@example.com" } | ConvertTo-Json)

$malFormado = Invoke-WebRequest "$api/contrasenas/recuperacion" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "esto-no-es-un-correo" } | ConvertTo-Json)

$existe.StatusCode                        # 202
$existe.Content -eq $noExiste.Content     # True
$existe.Content -eq $malFormado.Content   # True
```

Y **nada se encola para quien no existe**: encolarle un correo a una dirección no registrada
sería una fuga en sí misma, peor que la que se quería evitar.

```sql
SELECT Destinatario, Estado, Intentos, FechaEnvio FROM CorreoEnCola
WHERE Destinatario IN ('ana@example.com', 'nadie@example.com') ORDER BY CreadoEn DESC;
```

Sólo puede aparecer `ana@example.com`, con `Estado = 'Pendiente'`, `Intentos = 0` y
`FechaEnvio = NULL`: el correo **sale por la cola, no por SMTP** (RF-NOT-08). Repite el paso
con el SMTP apagado y funciona igual (§6.15).

Para sacar el código, léelo del correo encolado:

```sql
SELECT TOP 1 Cuerpo FROM CorreoEnCola
WHERE Destinatario = 'ana@example.com' ORDER BY CreadoEn DESC;
```

El valor que va detrás de `?codigo=` en el enlace es `$codigo`. En la base **sólo queda su
SHA-256**, nunca el valor en claro:

```sql
SELECT CONVERT(VARCHAR(64), CodigoHash, 2) AS HashHex, EmitidoEn, VenceEn, UsadoEn
FROM CodigoRecuperacion ORDER BY EmitidoEn DESC;
```

#### b) El código es de un solo uso y caduca (RF-CA-10)

**Usarlo dos veces se rechaza y la contraseña no cambia**:

```powershell
Invoke-RestMethod "$api/contrasenas/restablecer" -Method Post -ContentType "application/json" `
    -Body (@{ codigo = $codigo; contrasenaNueva = "NuevaClave2026" } | ConvertTo-Json)

Invoke-WebRequest "$api/contrasenas/restablecer" -Method Post -ContentType "application/json" `
    -Body (@{ codigo = $codigo; contrasenaNueva = "OtraClave2026" } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 422
```

Un código **caducado** se rechaza igual, con el mismo 422 y sin tocar la contraseña. Para
verlo sin esperar los 30 minutos, adelanta el vencimiento:

```sql
UPDATE CodigoRecuperacion SET VenceEn = DATEADD(minute, -1, SYSDATETIMEOFFSET());
```

> **Pedir la recuperación dos veces invalida el código anterior**: sólo el último emitido
> sirve. Y `GET /contrasenas/recuperacion?codigo=…`, que es la ruta a la que apunta el enlace
> del correo, **comprueba** el código sin gastarlo — para poder responder al enlace sin
> consumir lo que el usuario aún tiene que usar.

#### c) La contraseña nueva se guarda hasheada y la anterior deja de servir (RF-CA-11)

```sql
SELECT Correo, PasswordHash, PasswordCambiadoEn FROM Usuario WHERE Correo = 'ana@example.com';
```

`PasswordHash` no contiene `NuevaClave2026` en ningún sitio. Con la nueva se entra; con la
antigua, `POST /sesion/iniciar` responde 401.

#### d) Las sesiones abiertas antes del cambio dejan de servir (RF-CA-12)

Ésta es la parte que más se olvida. Abre sesión **antes** de restablecer y guarda la
credencial:

```powershell
$antes = Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = $clave } | ConvertTo-Json)
```

Restablece la contraseña y usa **esa credencial, la de antes**:

```powershell
Invoke-WebRequest "$api/sesion/yo" -Headers @{ Authorization = "Bearer $($antes.token)" } `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 401
```

Las sesiones están en la base, no dentro de un token autocontenido, y por eso se pueden
revocar:

```sql
SELECT CerradaEn, MotivoCierre FROM Sesion WHERE CerradaEn IS NOT NULL;
```

> **Las sesiones se cierran ANTES de guardar la contraseña nueva.** Si el proceso se cae
> entre los dos pasos, el peor desenlace es que las credenciales viejas ya no sirvan y el
> usuario tenga que entrar otra vez. Al revés, un fallo dejaría credenciales activas junto a
> una contraseña nueva, que es justo lo que RF-CA-12 viene a cerrar. Cerrar es la dirección en
> la que se falla.

#### e) Un Administrador puede forzar el restablecimiento (RF-CA-13)

```powershell
$admin = Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "admin@example.com"; contrasena = $claveAdmin } | ConvertTo-Json)

Invoke-RestMethod "$api/usuarios/restablecer-contrasena" -Method Post `
    -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($admin.token)" } `
    -Body (@{ usuarioId = $usuarioId } | ConvertTo-Json)
```

La contraseña anterior deja de servir **en el acto**, y las sesiones abiertas de ese usuario
se cierran. No hay ningún estado «pendiente de restablecer»: la contraseña se sustituye por
un valor aleatorio de 256 bits que no se guarda en ningún sitio y que nadie, tampoco el
Administrador, conoce. Después se encola un código al correo registrado, con el que el usuario
puede dejar la suya.

Un Estándar recibe **403**, incluso si construye la petición a mano sin pasar por ninguna
interfaz (RD-06):

```powershell
Invoke-WebRequest "$api/usuarios/restablecer-contrasena" -Method Post `
    -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($otro.token)" } `
    -Body (@{ usuarioId = $usuarioId } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 403
```

#### f) Cambiar la contraseña propia exige la actual (RF-CA-22)

```powershell
Invoke-WebRequest "$api/contrasenas/propia" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $token" } `
    -Body (@{ contrasenaActual = "ClaveEquivocada9"; contrasenaNueva = "NuevaClave2026" } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 422
```

Con la actual incorrecta el cambio **se rechaza y no toca nada**: ni cambia el hash, ni cierra
la sesión. La respuesta es **422 y no 401** — la identidad ya está establecida, así que un 401
haría creer al cliente que su credencial caducó (ver §6.13).

Con la actual correcta responde 200 y **cierra también la sesión que hizo la petición**, que es
lo que dice RF-CA-12; el mensaje de respuesta lo avisa para que el cliente no se entere por un
401 inesperado en la llamada siguiente.

La contraseña nueva tiene que cumplir la política de **RF-CA-14 en los tres caminos**:
recuperación, restablecimiento forzado y cambio propio.

#### Comprobación automática

```powershell
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~RecuperacionDeContrasenaTests"
```

Las 31 pruebas cubren los seis criterios de extremo a extremo sin simular nada: se habla HTTP
contra la aplicación real, el código se saca **del correo encolado** en lugar de inventarlo, y
el servidor SMTP está apagado durante toda la serie.

> **Una limitación que conviene saber.** La respuesta de pedir la recuperación es idéntica
> byte a byte, pero **no el tiempo**: para un correo registrado hay un insert y un encolado que
> no ocurren para uno inexistente. Igualarlo exigiría encolar también a las direcciones no
> registradas, que es una fuga mayor que la que se quería evitar. La enumeración por
> temporización queda aquí documentada como límite conocido, no resuelta.

### 6.15 Registrar un usuario funciona con el SMTP apagado (RF-NOT-08)

**Ésta es la prueba clave de la cola de correo.**

1. **Apaga el acceso al servidor SMTP**: deja `Correo__Host` vacío, o pon una contraseña
   deliberadamente incorrecta.
2. Registra un usuario con `POST /usuarios/registro` (ver §6.11).
3. El registro **debe funcionar**.
4. Comprueba que el correo quedó encolado, no enviado:

```sql
SELECT Destinatario, Asunto, Estado, Intentos, FechaEnvio
FROM CorreoEnCola ORDER BY CreadoEn DESC;
```

`Estado` debe ser `Pendiente` y `FechaEnvio` debe ser `NULL`.

### 6.16 El emisor de correo es un proceso aparte (RF-NOT-09)

```powershell
dotnet run --project tools/Biblioteca.Correo.Enviador
```

Debe aparecer en la consola:

```
now      Correos pendientes: 1
info     Correo <guid> entregado.
info     Proceso terminado. Enviados: 1. Con fallo: 0
```

### 6.17 Ejecutar el emisor dos veces no duplica correos (RF-NOT-12)

```powershell
dotnet run --project tools/Biblioteca.Correo.Enviador
dotnet run --project tools/Biblioteca.Correo.Enviador
```

**El segundo debe decir `No hay correos pendientes.`** y no enviar nada.

Cada envío empieza con un `UPDATE ... WHERE Estado = 'Pendiente'`. Si otro proceso ya
reclamó ese correo, la actualización afecta 0 filas y el envío se cancela.

Comprobación:

```sql
SELECT Estado, COUNT(*) FROM CorreoEnCola GROUP BY Estado;
```

Un correo enviado **no** aparece como `Pendiente` en la segunda ejecución.

### 6.18 El emisor se puede ejecutar sin que el proceso que encoló siga vivo

1. Registra un usuario con el SMTP apagado. El correo queda `Pendiente`.
2. Cierra la API por completo.
3. Enciende el SMTP: `Correo__Host`, `Correo__Usuario`, `Correo__Contrasena`.
4. `dotnet run --project tools/Biblioteca.Correo.Enviador`

El correo se envía. **Esto es lo que prueba RF-NOT-09**: el envío no depende del flujo
que creó el correo.

### 6.19 Los fallos internos no se filtran (RD-08)

```powershell
Invoke-RestMethod http://localhost:5XXX/usuarios -Headers @{ Authorization = "Bearer token-invalido" } -ErrorAction SilentlyContinue
```

La respuesta debe ser un `ProblemDetails` **sin** traza de pila, sin ruta de archivo, sin
nombre de tabla y sin la cadena de conexión. Sólo el mensaje del dominio.

### 6.20 Administrar usuarios: listado, cambio de rol y desactivación (RF-CA-04, 05, 06, 08, 20, 21)

Cuatro endpoints, todos reservados al Administrador:

| Método | Ruta | Criterio |
|---|---|---|
| `GET` | `/usuarios` | RF-CA-21, RF-CA-04 |
| `POST` | `/usuarios/cambiar-rol` | RF-CA-08, RF-CA-04 |
| `POST` | `/usuarios/desactivar` | RF-CA-20 |
| `POST` | `/usuarios/reactivar` | RF-CA-20 |

**No hay endpoint para crear al primer Administrador**, y es deliberado: un endpoint que
promoviera a Administrador sería una vía para que cualquiera se autoasignara el rol. El primer
Administrador se nombra sobre la base, una vez, y a partir de ahí ya se usa la API:

```sql
UPDATE Usuario SET Rol = 'Administrador' WHERE Correo = 'ana@example.com';
```

En lo que sigue, `$admin` es la sesión de un Administrador y `$socio` la de un Estándar, las dos
con §6.11 y §6.13 explican cómo tenerlas:

```powershell
$api = "http://localhost:5XXX"
$admin = Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "ana@example.com"; contrasena = $clave } | ConvertTo-Json)
$socio = Invoke-RestMethod "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "beto@example.com"; contrasena = $clave } | ConvertTo-Json)
$socioId = $socio.usuario.id
```

#### a) El listado muestra el rol y el estado, y no expone hashes ni tokens (RF-CA-21)

```powershell
$cuerpo = (Invoke-WebRequest "$api/usuarios" -Headers @{ Authorization = "Bearer $($admin.token)" }).Content
($cuerpo | ConvertFrom-Json) | Select-Object correo, rol, activo, creadoEn
```

Cada fila trae exactamente siete campos: `id`, `nombre`, `correo`, `rol`, `activo`, `creadoEn` y
`passwordCambiadoEn`. No hay ningún campo de hash, token ni código, y no es que se filtren: el
listado se proyecta en SQL a un `record ResumenDeUsuario` que no los tiene, así que
`PasswordHash` ni siquiera llega a salir de la base. Para verlo en la respuesta completa:

```powershell
$cuerpo -match "hash"      # False
$cuerpo -match "token"     # False
```

Y para verlo en la base, al lado de un usuario normal:

```sql
SELECT TOP 1 Correo, LEN(PasswordHash) AS LongitudDelHash FROM Usuario ORDER BY CreadoEn DESC;
```

Un Estándar recibe **403**, incluso con la petición escrita a mano (RF-CA-06, RD-06):

```powershell
Invoke-WebRequest "$api/usuarios" -Headers @{ Authorization = "Bearer $($socio.token)" } `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 403
```

#### b) Un Estándar no cambia ningún rol, ni el suyo (RF-CA-08)

Las dos frases del criterio son la misma comprobación. En cuanto se declara la operación
`UsuariosCambiarRol`, que `PoliticaDeOperaciones` reserva al Administrador (§6.10), las dos
peticiones mueren en el guard **antes de tocar nada**:

```powershell
# Su propio rol:
Invoke-WebRequest "$api/usuarios/cambiar-rol" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($socio.token)" } `
    -Body (@{ usuarioId = $socioId; rol = "Administrador" } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 403

# El de otro:
Invoke-WebRequest "$api/usuarios/cambiar-rol" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($socio.token)" } `
    -Body (@{ usuarioId = $usuarioId; rol = "Administrador" } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 403
```

No hay ninguna regla especial para «el propio». La misma los cubre a los dos, que es lo que
pide el criterio: que el rechazo no dependa de a quién se dirige.

#### c) Un Administrador cambia el rol (RF-CA-08, RF-CA-04)

```powershell
Invoke-RestMethod "$api/usuarios/cambiar-rol" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($admin.token)" } `
    -Body (@{ usuarioId = $socioId; rol = "Administrador" } | ConvertTo-Json)
# {"usuarioId":"...","rolAnterior":"Estandar","rolNuevo":"Administrador","sesionesCerradas":1}
```

Una cosa que conviene mirar después:

```powershell
Invoke-WebRequest "$api/usuarios" -Headers @{ Authorization = "Bearer $($socio.token)" } `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 401: su credencial lleva
                                                            # el rol viejo dentro
```

La credencial de `$socio` **deja de servir**, y por dos motivos distintos según hacia dónde
fuese el cambio: un Estándar ascendido necesitaría entrar otra vez para que su credencial
reflejara el ascenso, y un Administrador degradado conservaría el acceso a la administración
hasta que venciera su credencial, que es un agujero de seguridad. Cerrar las sesiones evita las
dos cosas. Por eso la respuesta dice cuántas se han cerrado.

El rol **sustituye** al anterior, no se añade al lado. «Todo usuario tiene exactamente un rol»
(RF-CA-04) no es una promesa del código: es una consecuencia de la forma de la operación,
porque no existe el camino que deje dos. El enum tiene exactamente dos valores, y una prueba
comprueba que todo valor del enum tiene sitio en la política de operaciones:

```powershell
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~AdministracionDeUsuariosTests"
```

Un rol que no existe se rechaza con **422** y el motivo de la pieza, no con el 400 opaco de la
deserialización:

```powershell
Invoke-WebRequest "$api/usuarios/cambiar-rol" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($admin.token)" } `
    -Body (@{ usuarioId = $socioId; rol = "Superusuario" } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 422
```

Los tres rechazos del endpoint usan la misma distinción, y conviene tenerla presente porque
**«ese rol no existe» y «el cuerpo está mal escrito» no son lo mismo**:

| Cuerpo | Código | Por qué |
|---|---|---|
| `{"rol": "Superusuario"}` | **422** | La forma es correcta; el rol que nombra no existe |
| `{"rol": 7}` | **400** | El campo es un nombre de rol, no un número |
| `{}` | **400** | Falta un campo obligatorio |

Un 7 no es un rol escrito de otra manera, es otra cosa. Con el enum en la firma del cuerpo, las
dos primeras filas devolvían el mismo 400 de «no se pudo convertir», acompañado además de que
falta el cuerpo entero —porque al fallar el enlace el resto del modelo queda en `null`—, que es un
mensaje que no habla de un rol inexistente. El campo es texto ahora y lo traduce la acción, que
es lo que le toca al host (RD-02); decidir qué roles existen sigue siendo de la pieza
(`Enum.IsDefined` en `CambiarRolAsync`).

Cambiar un usuario a un rol que ya tiene también da 422, y **no le cierra las sesiones**: un
cambio que no cambia nada no debe expulsar a nadie.

#### d) Desactivar y reactivar (RF-CA-20)

El criterio tiene tres partes y las tres se comprueban aquí:

```powershell
# 1. Desactivar. Responde cuántas sesiones se han cerrado.
Invoke-RestMethod "$api/usuarios/desactivar" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($admin.token)" } `
    -Body (@{ usuarioId = $socioId } | ConvertTo-Json)
# {"usuarioId":"...","activo":false,"sesionesCerradas":1}

# 2. La sesión que ya estaba abierta deja de servir.
Invoke-WebRequest "$api/sesion/yo" -Headers @{ Authorization = "Bearer $($socio.token)" } `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 401

# 3. Y tampoco puede iniciar sesión otra vez.
Invoke-WebRequest "$api/sesion/iniciar" -Method Post -ContentType "application/json" `
    -Body (@{ correo = "beto@example.com"; contrasena = $clave } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 401
```

Y un Administrador **no puede desactivarse a sí mismo**, que es la tercera frase del criterio:

```powershell
Invoke-WebRequest "$api/usuarios/desactivar" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($admin.token)" } `
    -Body (@{ usuarioId = $admin.usuario.id } | ConvertTo-Json) `
    -SkipHttpErrorCheck | Select-Object StatusCode        # 422
```

Es **422 y no 403**, y la diferencia es el fondo: el que llama está autorizado, y es el único
que puede hacer esto a cualquiera. Lo que no puede es aplicar el signo de menos sobre su
propia cuenta. Con 403 se le diría «no estás autorizado», que es falso.

La comprobación vive en el servicio, no en el endpoint, y el identificador de quien llama sale
del claim que escribió el autenticador, nunca del cuerpo de la petición: si fuera un parámetro,
la única comprobación de seguridad del sistema dependería de un dato que elige el cliente.

Reactivar es el camino de vuelta, y **no devuelve las sesiones anteriores**: el usuario tiene
que entrar otra vez.

```powershell
Invoke-RestMethod "$api/usuarios/reactivar" -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($admin.token)" } `
    -Body (@{ usuarioId = $socioId } | ConvertTo-Json)
# {"usuarioId":"...","activo":true,"sesionesCerradas":0}
```

> **Reactivar no levanta el bloqueo por cinco intentos fallidos (RF-CA-19).** Desactivar y
> bloquear son dos cosas distintas, y reactivar no promete la segunda: el bloqueo expira solo a
> los quince minutos. Hay una prueba que lo fija, con un usuario de control que sí puede entrar
> después de reactivarlo — sin ese control, la comprobación no probaría nada.

> **La cuenta desactivada se rechaza en cada petición, no sólo al iniciar sesión.** El
> autenticador comprueba `Usuario.Activo` cada vez que resuelve una credencial, además de que
> desactivar cierre las sesiones. Es la segunda red: si alguien desactiva una cuenta por un
> camino que no pase por el servicio (SQL, una consola de soporte, un endpoint futuro), la
> credencial sigue sin servir. Antes de este *pull request* el comentario del propio
> autenticador ya prometía esa comprobación y el código no la hacía.

#### Comprobación automática

```powershell
dotnet test tests/Biblioteca.Api.Tests --filter "FullyQualifiedName~AdministracionDeUsuariosTests"
```

Las 29 pruebas hablan HTTP contra la aplicación real y miran la base de verdad. Las que más
importan son las de rechazo: las cuatro operaciones de administración se barren con un Estándar
—con la petición construida a mano, sin los ayudantes del cliente—, sin credencial y con una
credencial cerrada. Son las que vigilan el hilo de §6.10.

> **Un fallo que estas pruebas destaparon.** El proyecto tenía
> `ConfigureHttpJsonOptions(... Add(new JsonStringEnumConverter()))`, que configura el
> serializador de las APIs mínimas y de `HttpResponse.Json`, **no** el de los controladores. Como
> todas las respuestas de esta API salen de controladores, esa línea no hacía nada. El síntoma era
> que `POST /usuarios/cambiar-rol` con `{"rol": "Administrador"}` devolvía 400, y el listado
> devolvía `"rol": 0` mientras `/sesion/iniciar` devolvía `"rol": "Estandar"`: dos endpoints con
> el mismo enum, de dos maneras distintas, en la misma API. El converter se movió a
> `AddJsonOptions`. Es el tercer caso de la misma familia que §6.10 y que el autenticador: código
> que no hace lo que su comentario o su configuración prometen. Los tres se encontraron **escribiendo
> las pruebas** de este *pull request*, no leyendo el código con calma.

#### Qué NO incluye

- **Que un Administrador no pueda degradarse a sí mismo.** El enunciado lo prohíbe
  explícitamente para desactivar (RF-CA-20) y **no** lo dice para cambiar el rol (RF-CA-08). La
  asimetría es del enunciado, no un descuido, y no se inventa una regla que no está escrita. Si
  algún día se pide, es una comprobación en `CambiarRolAsync`, igual que la de
  autodesactivación.
- **Auditoría de quién cambió el rol de quién y cuándo.** Es materia de la semana 14, pieza 6
  (ver §8). El sitio es `ServicioDeAdministracion`, que ya recibe el `actorId` en desactivar y
  donde habrá que añadirlo en los otros dos métodos.

---

## 7. Dónde está cada cosa

### Requisitos → código

| Requisito | Dónde está |
|---|---|
| **RF-CA-05** — un solo punto de exigencia de rol | `src/Biblioteca.Identidad/PoliticaDeOperaciones.cs` |
| RF-CA-05 — que no se pueda saltar | `src/Biblioteca.Api/Seguridad/ValidarOperacionesDeclaradasConvention.cs` |
| RF-CA-05 — que la exigencia de rol se evalúe de verdad | `RequisitoDeOperacion`, en la política de repliego de `Program.cs` + `RequisitoDeOperacionHandler` (§6.10) |
| RF-CA-04 — dos roles, y todo usuario tiene exactamente uno | `Rol` (enum de dos valores) + `ServicioDeAdministracion.CambiarRolAsync`, que **sustituye** el rol |
| RF-CA-04 — que ningún rol quede fuera de la tabla | `PoliticaDeOperaciones.Requisitos` + prueba `Todo_rol_del_enum_aparece_en_la_politica_de_operaciones` |
| **RF-CA-06 / RD-06** — el Estándar recibe un rechazo explícito | `[RequiereOperacion]` en la acción + `RequisitoDeOperacionHandler`, evaluado en la política de repliego (§6.10, §6.20) |
| **RF-CA-21** — el listado con rol y estado | `GET /usuarios` + `ServicioDeAdministracion.ListarAsync`, proyectado a `ResumenDeUsuario` en SQL |
| RF-CA-21 — el listado nunca lleva hashes ni tokens | `ResumenDeUsuario` no tiene ningún campo de ese tipo, y la proyección ocurre en SQL: `PasswordHash` no sale de la base |
| **RF-CA-08** — cambio de rol, sólo Administrador | `ServicioDeAdministracion.CambiarRolAsync` + `POST /usuarios/cambiar-rol` |
| RF-CA-08 — ni el propio ni el ajeno | El guard rechaza antes de ejecutar; no hace falta una regla aparte (§6.20b) |
| RF-CA-08 — el rol de la credencial no se queda viejo | `IServicioDeSesiones.CerrarTodasAsync` en el mismo cambio, con el mismo contexto |
| **RF-CA-20** — desactivar y reactivar | `ServicioDeAdministracion.DesactivarAsync` / `.ReactivarAsync` + `POST /usuarios/desactivar` y `/reactivar` |
| RF-CA-20 — la cuenta inactiva no entra | `ServicioDeAcceso.AutenticarAsync`, comprobado **después** de la contraseña |
| RF-CA-20 — sus credenciales dejan de servir | `IServicioDeSesiones.CerrarTodasAsync` **y** la revalidación de `Usuario.Activo` en `AutenticacionPorSesion`, en cada petición |
| RF-CA-20 — no se puede desactivar a sí mismo | `DesactivarAsync`, con el `actorId` que sale del claim, no del cuerpo (§6.20d) |
| **RF-CA-04 / 06 / 08 / 20 / 21** — las cuatro acciones | `UsuariosController.Listar` / `.CambiarRol` / `.Desactivar` / `.Reactivar` |
| **RF-CA-04 / 06 / 08 / 20 / 21** — comprobación automática | `tests/Biblioteca.Api.Tests/AdministracionDeUsuariosTests.cs` (29 pruebas) |
| RF-CA-02 — hash con sal por usuario | `PasswordHasher<Usuario>`, inyectado en `ServicioDeRegistro` (decisión en `docs/diseno-de-componentes.md`, §4.5) |
| RF-CA-14 — política de contraseñas | `src/Biblioteca.Identidad/Password/PoliticaDeContrasenas.cs` |
| RF-CA-01 — correo único | Índice `UQ_Usuario_Correo`, aplicado en `IdentidadDbContext.cs` |
| RF-CA-01 — el rechazo, no sólo el índice | `ServicioDeRegistro.RegistrarAsync` (comprobación previa + traducción de la carrera del índice) |
| **RF-CA-15** — la cuenta nace inactiva | `ServicioDeRegistro.RegistrarAsync` (`Activo = false`) |
| **RF-CA-16** — enlace de un solo uso | `TokenActivacion.EstaVigente` + `ServicioDeRegistro.ActivarAsync` |
| **RF-CA-17** — reenvío sin revelar correos | `ServicioDeRegistro.ReenviarActivacionAsync` |
| **RF-CA-01/02/14/15/16/17** — las tres acciones | `src/Biblioteca.Api/Controllers/UsuariosController.cs` |
| **RF-CA-01/02/14/15/16/17** — comprobación automática | `tests/Biblioteca.Api.Tests/RegistroYActivacionTests.cs` (19 pruebas) |
| **RF-CA-03** — inicio de sesión | `ServicioDeAcceso.AutenticarAsync` + `SesionController.Iniciar` |
| RF-CA-03 — rechazo que no revela qué falló | `ServicioDeAcceso.CredencialesInvalidas` + hash señuelo por temporización |
| RF-CA-03 — 401 y no 403 | `TipoError.NoAutenticado` → `ManejadorDeExcepciones` |
| **RF-CA-07** — consulta del autenticado | `GET /sesion/yo` + `ServicioDeAcceso.ConsultarYoAsync` |
| RF-CA-15 / 16 — token de activación | `TokenActivacion` + `GeneradorDeSecretos` |
| **RF-CA-15** — la cuenta inactiva no entra | `ServicioDeAcceso.AutenticarAsync`, comprobado **después** de la contraseña |
| **RF-CA-18** — el cierre es efectivo | `SesionController.Cerrar` → `IServicioDeSesiones.CerrarAsync` |
| RF-CA-12 / 18 / 20 — revocación | `IServicioDeSesiones.CerrarAsync` / `.CerrarTodasAsync` |
| **RF-CA-19** — bloqueo tras 5 intentos | `ServicioDeAcceso.AutenticarAsync` + `Usuario.IntentosFallidos` / `BloqueadoHasta` |
| **RF-CA-09** — la respuesta no revela qué correos existen | `ContrasenasController.IniciarRecuperacion` (202 fijo) + `ServicioDeContrasenas.IniciarRecuperacionAsync` (devuelve `void`) |
| **RF-CA-10** — código de un solo uso, con vencimiento | `CodigoRecuperacion.EstaVigente` + `ServicioDeContrasenas.EmitirCodigoAsync` / `.BuscarCodigoVigenteAsync` |
| RF-CA-10 — el enlace del correo no gasta el código | `GET /contrasenas/recuperacion` → `.ComprobarCodigoAsync` |
| RF-CA-10 — sale por la cola, no por SMTP | `ServicioDeContrasenas.EmitirCodigoAsync` → `IEncolaCorreo`, `Plantilla = "recuperacion"` |
| **RF-CA-11** — la nueva sirve y la anterior deja de servir | `ServicioDeContrasenas.SustituirContrasenaYCerrarSesionesAsync` (`PasswordHasher<Usuario>`) |
| **RF-CA-12** — se cierran las sesiones abiertas | `IServicioDeSesiones.CerrarTodasAsync`, con el mismo contexto del cambio |
| **RF-CA-13** — restablecimiento forzado | `ServicioDeContrasenas.ForzarRestablecimientoAsync` + `POST /usuarios/restablecer-contrasena` |
| **RF-CA-22** — cambio propio con la actual | `ServicioDeContrasenas.CambiarPropiaAsync` + `POST /contrasenas/propia` |
| RF-CA-14 — la política se cumple en los tres caminos | `PoliticaDeContrasenas.Validar`, llamado antes de tocar la base en cada uno |
| RF-CA-09/10/11/12/22 — las cinco acciones | `src/Biblioteca.Api/Controllers/ContrasenasController.cs` |
| RF-CA-09/10/11/12/13/22 — comprobación automática | `tests/Biblioteca.Api.Tests/RecuperacionDeContrasenaTests.cs` (31 pruebas) |
| **RF-NOT-08** — encolar, no enviar | `EncolaCorreo` + puerto `IEncolaCorreo` (que vive en el Core, §4) |
| **RF-NOT-09** — envío en proceso aparte | `tools/Biblioteca.Correo.Enviador` |
| **RF-NOT-12** — sin envíos duplicados | `SmtpEntregador.EntregarAsync` (reclamo condicional) |
| RF-NOT-13 — credenciales SMTP del entorno | `OpcionesCorreo` + `appsettings.json` (sólo nombres) |
| **RF-NEG-03/04/05** — máquina de estados | `docs/maquina-de-estados.md` + `src/Biblioteca.Biblioteca/Prestamos/` |
| RF-NEG-01 — las relaciones existen en el modelo de datos, no sólo en el diagrama | `BibliotecaDbContext.cs` + `tests/Biblioteca.Biblioteca.Tests/RelacionesDelModeloTests.cs` |
| RD-03 — el Core no depende del negocio | Sección 4 de este README; §6.2 lo comprueba también en el modelo de datos |
| RD-07 — validación de entrada | `PoliticaDeContrasenas`, `EncolaCorreo` |
| RD-08 — errores sin filtrar | `ManejadorDeExcepciones` |
| RD-10 — sin secretos versionados | `.gitignore`, `.env.example`, `appsettings.json` |
| RD-11 — un solo criterio de reloj | `TimeProvider`, registrado en `Program.cs` |
| RD-12 — probar sin la aplicación completa | Las 4 series en `tests/` |

### Documentos

| Documento | Qué contiene |
|---|---|
| [`docs/diseno-de-componentes.md`](docs/diseno-de-componentes.md) | Mapa de componentes, reglas de dependencia, decisiones justificadas, qué NO incluye cada uno |
| [`docs/maquina-de-estados.md`](docs/maquina-de-estados.md) | Estados, transiciones permitidas y prohibidas, diagrama |
| [`.env.example`](.env.example) | Nombres de las variables de entorno y su propósito. **Sin valores.** |

---

## 8. Qué NO incluye esta práctica

Para que quede claro que es alcance, y no omisión:

| Fuera de alcance | Cuándo entra |
|---|---|
| Auditoría persistente (RF-AUD-*) | Semana 14, pieza 6 |
| Registros de auditoría de RF-CA-08, RF-CA-13 y RF-CA-20 | Semana 14, pieza 6 |
| Prueba completa de la máquina de estados del negocio | Semana 8. Aquí sólo la estructura |
| Gestión de permisos de documentos (RF-CA-16 *del Core*, pieza 2 — no confundir con el enlace de un solo uso de esta práctica) | Semanas 6-8 |
| Manejador de documentos (RF-DOC-*) | Semana 9 |
| Notificaciones al usuario y plantillas de correo (RF-NOT-01..07) | Semanas 7-8 |
| Reportes (RF-REP-*) | Semana 12 |
| Préstamos, devoluciones y disponibilidad (lógica de negocio) | Semanas posteriores |

### Endpoints disponibles ahora

| Método | Ruta | Autenticación | Propósito |
|---|---|---|---|
| `GET` | `/salud` | Ninguna | Comprobación de vida del servicio |
| `POST` | `/usuarios/registro` | Ninguna | Alta de cuenta; queda inactiva hasta abrir el enlace |
| `GET` | `/usuarios/activar?token=…` | Ninguna | Apertura del enlace de un solo uso |
| `POST` | `/usuarios/reenviar-activacion` | Ninguna | Reenvío del enlace, sin revelar qué correos existen |
| `POST` | `/sesion/iniciar` | Ninguna | Inicia sesión y entrega la credencial (RF-CA-03, 15, 19) |
| `GET` | `/sesion/yo` | Credencial de sesión | Usuario autenticado y su rol (RF-CA-07) |
| `POST` | `/sesion/cerrar` | Credencial de sesión | Cierra la sesión e invalida la credencial (RF-CA-18) |
| `POST` | `/contrasenas/recuperacion` | Ninguna | Pide un código de un solo uso, sin revelar qué correos existen (RF-CA-09, 10) |
| `GET` | `/contrasenas/recuperacion?codigo=…` | Ninguna | Abre el enlace del correo; comprueba el código **sin gastarlo** (RF-CA-10) |
| `POST` | `/contrasenas/restablecer` | Ninguna | Define la contraseña con el código y cierra las sesiones (RF-CA-11, 12) |
| `POST` | `/contrasenas/propia` | Credencial de sesión | Cambia la contraseña indicando la actual (RF-CA-22) |
| `POST` | `/usuarios/restablecer-contrasena` | Credencial de sesión, rol Administrador | Restablecimiento forzado; mata la contraseña anterior (RF-CA-13) |
| `GET` | `/usuarios` | Credencial de sesión, rol Administrador | Lista los usuarios con su rol y su estado, sin hashes ni tokens (RF-CA-21) |
| `POST` | `/usuarios/cambiar-rol` | Credencial de sesión, rol Administrador | Sustituye el rol de un usuario y cierra sus sesiones (RF-CA-08) |
| `POST` | `/usuarios/desactivar` | Credencial de sesión, rol Administrador | Desactiva la cuenta y mata sus credenciales (RF-CA-20) |
| `POST` | `/usuarios/reactivar` | Credencial de sesión, rol Administrador | Reactiva la cuenta; no devuelve sus sesiones ni levanta el bloqueo (RF-CA-20) |

Las tres acciones de registro, `POST /sesion/iniciar` y las tres de recuperación son públicas
a propósito: todavía no hay identidad que exigir. Es el mismo tratamiento que recibe `/salud`
en el guard de arranque (`ValidarOperacionesDeclaradasConvention` exceptúa las acciones
`[AllowAnonymous]`), y no es una excepción a RF-CA-05 sino su caso honesto. Todas las demás
acciones del sistema siguen declarando su operación **y son evaluadas en cada petición** (§6.10).

**No hay endpoint para crear al primer Administrador.** Un endpoint que promoviera a
Administrador sería la vía más corta para que cualquiera se autoasignara el rol, así que el
primer Administrador se nombra una vez sobre la base (§6.20) y a partir de ahí se usa `POST
/usuarios/cambiar-rol`.

### Pruebas

```powershell
dotnet test Biblioteca.sln
```

| Serie | Pruebas | Qué cubren |
|---|---:|---|
| `Biblioteca.Nucleo.Tests` | 10 | Tipos de error, contrato de auditoría |
| `Biblioteca.Identidad.Tests` | 44 | Política de operaciones (RF-CA-05), guard de arranque, contraseñas (RF-CA-14), generación de tokens |
| `Biblioteca.Biblioteca.Tests` | 32 | Estructura de la máquina de estados (RF-NEG-03/04/05) y relaciones del modelo de datos (RF-NEG-01, RD-03) |
| `Biblioteca.Api.Tests` | 107 | Arranque real contra SQL Server, barrido de RF-CA-05 sobre los controladores reales, registro y activación de extremo a extremo (RF-CA-01, 02, 14, 15, 16, 17), sesión de extremo a extremo (RF-CA-03, 07, 18, 19), contraseñas de extremo a extremo (RF-CA-09, 10, 11, 12, 13, 22) y administración de usuarios de extremo a extremo (RF-CA-04, 06, 08, 20, 21) |
| **Total** | **193** | |
