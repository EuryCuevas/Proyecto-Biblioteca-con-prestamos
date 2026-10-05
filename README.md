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



### 6.14 Registrar un usuario funciona con el SMTP apagado (RF-NOT-08)

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

### 6.15 El emisor de correo es un proceso aparte (RF-NOT-09)

```powershell
dotnet run --project tools/Biblioteca.Correo.Enviador
```

Debe aparecer en la consola:

```
now      Correos pendientes: 1
info     Correo <guid> entregado.
info     Proceso terminado. Enviados: 1. Con fallo: 0
```

### 6.16 Ejecutar el emisor dos veces no duplica correos (RF-NOT-12)

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

### 6.17 El emisor se puede ejecutar sin que el proceso que encoló siga vivo

1. Registra un usuario con el SMTP apagado. El correo queda `Pendiente`.
2. Cierra la API por completo.
3. Enciende el SMTP: `Correo__Host`, `Correo__Usuario`, `Correo__Contrasena`.
4. `dotnet run --project tools/Biblioteca.Correo.Enviador`

El correo se envía. **Esto es lo que prueba RF-NOT-09**: el envío no depende del flujo
que creó el correo.

### 6.18 Los fallos internos no se filtran (RD-08)

```powershell
Invoke-RestMethod http://localhost:5XXX/usuarios -Headers @{ Authorization = "Bearer token-invalido" } -ErrorAction SilentlyContinue
```

La respuesta debe ser un `ProblemDetails` **sin** traza de pila, sin ruta de archivo, sin
nombre de tabla y sin la cadena de conexión. Sólo el mensaje del dominio.

---

## 7. Dónde está cada cosa

### Requisitos → código

| Requisito | Dónde está |
|---|---|
| **RF-CA-05** — un solo punto de exigencia de rol | `src/Biblioteca.Identidad/PoliticaDeOperaciones.cs` |
| RF-CA-05 — que no se pueda saltar | `src/Biblioteca.Api/Seguridad/ValidarOperacionesDeclaradasConvention.cs` |
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
| RF-CA-10 — código de recuperación | `CodigoRecuperacion` |
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

Las tres acciones de registro y `POST /sesion/iniciar` son públicas a propósito: todavía no
hay identidad que exigir. Es el mismo tratamiento que recibe `/salud` en el guard de
arranque (`ValidarOperacionesDeclaradasConvention` exceptúa las acciones
`[AllowAnonymous]`), y no es una excepción a RF-CA-05 sino su caso honesto. Todas las demás
acciones del sistema siguen declarando su operación.

Los endpoints de la pieza 1 que faltan (cambio de contraseña, recuperación, administración
de usuarios) llegan en los siguientes *pull requests*. Cada uno se documenta aquí con su
criterio de aceptación, en la sección 6.

### Pruebas

```powershell
dotnet test Biblioteca.sln
```

| Serie | Pruebas | Qué cubren |
|---|---:|---|
| `Biblioteca.Nucleo.Tests` | 10 | Tipos de error, contrato de auditoría |
| `Biblioteca.Identidad.Tests` | 44 | Política de operaciones (RF-CA-05), guard de arranque, contraseñas (RF-CA-14), generación de tokens |
| `Biblioteca.Biblioteca.Tests` | 32 | Estructura de la máquina de estados (RF-NEG-03/04/05) y relaciones del modelo de datos (RF-NEG-01, RD-03) |
| `Biblioteca.Api.Tests` | 47 | Arranque real contra SQL Server, barrido de RF-CA-05 sobre los controladores reales, registro y activación de extremo a extremo (RF-CA-01, 02, 14, 15, 16, 17) y sesión de extremo a extremo (RF-CA-03, 07, 18, 19) |
| **Total** | **133** | |
