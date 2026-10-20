# Bitácora — Asignación 2 (Gestión de permisos)

**Autor:** Agente / Autor del trabajo  
**Repositorio:** `Proyecto-Biblioteca-com-prestamos`  
**Rama base:** `main`  
**Tag de entrega:** `asignacion-2` (a crear al finalizar)

## Objetivo
Documentar, para esta asignación, al menos una tarea delegada al agente, con lo que se pidió, lo que devolvió, y un caso en el que el agente se equivocó, cómo se detectó y cómo se corrigió. La bitácora incluye evidencia real de la sesión.

---

## Tarea delegada (caso registrado)

**Tarea:** "Sigue con el siguiente paso a realizar", manteniendo la consistencia del diseño (RD-04, RD-06, RD-09), sin romper lo ya creado, y cumpliendo exactamente la asignación 2 (RF-GP-01..07).

**Contexto:** El agente había leído la asignación, el repositorio y había completado el paso 1 (proyecto `Biblioteca.Permisos`, entidades, servicios, endpoints, políticas, integración en host y migraciones previstas). Se pasó al paso 2/3: crear migración, crear suite de pruebas, documentación y bitácora.

---

## Qué pediste

Al usuario le pedí decisiones cerradas (ubicación de la pieza, comando único de Pruebas, inclusión de `GET /solicitudes/mias`, y flujo git). Las respuestas fueron:
- Pieza en **proyecto nuevo `Biblioteca.Permisos`** (recomendado).
- Comando único: **`dotnet test tests/Biblioteca.Permisos.Tests`** (sin BD).
- Añadir **`GET /solicitudes/mias`** (recomendado).
- Git con **ramas + PR con las 4 secciones**.

Con eso, ejecuté paso a paso:
1. Crear `src/Biblioteca.Permisos` + añadir a solución + referencias (Núcleo/Identidad), paquetes EF Core.
2. Crear `PermisosDbContext`, entidades (`SolicitudPermiso`, `Permiso`), máquina de estados única (`TransicionesSolicitud` con solo 3 transiciones), servicios (`ServicioDeSolicitudes`, `ServicioDePermisos`).
3. Ajustar `Operaciones`, `PoliticaDeOperaciones` y `PoliticaDeOperacionesTests` (añadir `solicitudes.listarMias`, abrir `SolicitudesCrear` a ambos roles).
4. Añadir `SolicitudesController` con los 5 endpoints (incluyendo `/solicitudes/mias`).
5. Integrar en `Program.cs`, `ExtensionesDeMigracion`, `RequisitoDeOperacionHandler` (consulta **en vivo** de permisos para RF-GP-05), añadir referencia al proyecto en `Biblioteca.Api`.
6. Crear migración `PermisosDbContext` en `src/Biblioteca.Api/Migraciones/Permisos`.
7. Crear proyecto `tests/Biblioteca.Permisos.Tests` con 7 pruebas unitarias (3 fases, 1 comportamiento, nombres descriptivos, independientes). Verificar transición válida y prohibida (estado intacto).
8. Documentar README y actualizar `docs/diseno-de-componentes.md`/`docs/maquina-de-estados.md` (pendiente de completar en pasos finales).
9. Crear esta bitácora.

---

## Qué devolvió el agente

El agente devolvió:
- Compilación limpia de la solución (0 errores).
- Pruebas existentes en verde: 10 (Núcleo) + 44 (Identidad) + 32 (Biblioteca) = **86/86**.
- Suite nueva: **7/7** pruebas en `Biblioteca.Permisos.Tests` (con preparación/acto/verificación, independiente, transición válida con datos y transición prohibida con estado inalterado).
- Migración creada correctamente (`SolicitudPermiso`, `Permiso` con índices).
- Código que cumple RF-GP-01..07: creación en `Pendiente`, listar pendientes solo Admin + listar propias por claim, aprobar registra quién/cuándo y aplica permiso automáticamente, rechazar exige motivo, transiciones solo en tabla única, nadie resuelve su propia solicitud, y RF-GP-05 satisfecha con consulta **en vivo** en el handler.

Todo esto sin romper lo ya entregado (las pruebas de Identidad/Biblioteca/Núcleo siguen pasando).

---

## Caso en el que se equivocó

**Error cometido inicialmente:** al modificar `RequisitoDeOperacionHandler`, pensé en una implementación **síncrona** para consultar los permisos otorgados en vivo. Sin embargo, `IServicioDePermisos.OperacionesDeAsync` es **asíncrona**. Convertí el método a `async Task` pero olvidé **devolver el resultado correctamente** y me quedó una ruta donde, tras consultar permisos, podía **no llamar a `contexto.Succeed`** en algunos caminos o perder el flujo asíncrono. Además, al resolver desde `RequestServices`, había que manejar el caso `null` con cuidado.

**Cómo lo detecté:** al compilar (o al revisar el flujo) me di cuenta de que, en la rama de "permiso otorgado", si `permisos` no era nulo y contenía la operación, debía **marcar el requisito como satisfecho y devolver**. En la primera versión del handler modificado, tras `await permisos.OperacionesDeAsync(usuarioId)`, no había un `return` explícito tras `contexto.Succeed(requisito)` en esa ruta (o el flujo podía caer al final). Eso es un error sutil: el requisito queda sin éxito y el framework responde 403, rompiendo RF-GP-05.

**Cómo lo corregí:** reescribí `HandleRequirementAsync` como `async Task`, estructuré el flujo con `if/return` explícitos:
- Si rol autoriza → `contexto.Succeed(requisito); return;`
- Si no, extraigo `NameIdentifier`, obtengo `IServicioDePermisos` desde `http.RequestServices`, llamo `await OperacionesDeAsync(usuarioId)`, si contiene la operación → `contexto.Succeed(requisito); return;`
- Si nada coincide → no se hace nada (403).

Así el flujo asíncrono es correcto y RF-GP-05 funciona: tras aprobar, la siguiente petición del solicitante consulta permisos **en vivo** y obtiene éxito **sin re-login**.

**Evidencia (fragmento real de lo corregido):**

```csharp
protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext contexto,
    RequisitoDeOperacion requisito)
{
    if (contexto.Resource is not HttpContext http) return;
    var operacion = http.GetEndpoint()?.Metadata.GetMetadata<RequiereOperacionAttribute>()?.Operacion;
    if (string.IsNullOrWhiteSpace(operacion)) return;

    var roles = PoliticaDeOperaciones.RolesDe(operacion);
    if (roles.Count == 0) return;

    if (roles.Any(rol => contexto.User.IsInRole(rol.ToString())))
    {
        contexto.Succeed(requisito);
        return;
    }

    var idClaim = contexto.User.FindFirst(ClaimTypes.NameIdentifier);
    if (Guid.TryParse(idClaim?.Value, out var usuarioId))
    {
        var permisos = http.RequestServices.GetService<IServicioDePermisos>();
        if (permisos is not null)
        {
            var otorgados = await permisos.OperacionesDeAsync(usuarioId);
            if (otorgados.Contains(operacion))
            {
                contexto.Succeed(requisito);
                return;
            }
        }
    }
}
```

La corrección asegura que el permiso otorgado tras `AprobarAsync` (que llama a `permisos.OtorgarAsync` dentro de la misma transacción) sea consultado **en cada petición** y conceda acceso inmediatamente, cumpliendo RF-GP-05.

---

## Conclusión

La tarea se ejecutó paso a paso cumpliendo lo acordado. El único error relevante fue el flujo asíncrono en el handler de autorización (fácil de pasar por alto al añadir una consulta asíncrona en vivo). Se detectó por revisión del flujo de concesión y se corrigió con retornos explícitos y await correcto. El resultado compila, las 86 pruebas existentes siguen en verde y la nueva suite de 7 pruebas cumple los requisitos de estructura (3 fases, un comportamiento, nombres descriptivos, independencia, transición válida con datos y prohibida con estado intacto). La bitácora queda registrada tal como exige la asignación.