# Máquina de estados del módulo de negocio — Biblioteca

**RF-NEG-03, RF-NEG-04, RF-NEG-05, RD-04.**

> **Alcance de la Práctica 1.** Se entrega la **estructura** de la máquina de estados:
> los estados, las transiciones permitidas, las prohibidas y los estados terminales, más
> las pruebas que verifican que esa estructura es coherente. La lógica que decide si una
> petición concreta puede ejecutarse (disponibilidad de ejemplares, deuda, vencimientos)
> se desarrolla más adelante; la prueba completa de esta máquina llega en la **semana 8**.
>
> **Nada en este documento es lógica de negocio todavía.** Los estados existen, las
> transiciones están declaradas y las prohibiciones son explícitas, pero todavía no
> hay endpoints que las ejecuten.

---

## 1. Entidad y atributo de estado

El préstamo es la entidad central del módulo de negocio.

| Atributo | Tipo | Notas |
|---|---|---|
| `Id` | `Guid` | Identificador del préstamo |
| `SocioId` | `Guid` | Socio que solicita |
| `EjemplarId` | `Guid` | Ejemplar concreto que se presta |
| `Estado` | `EstadoPrestamo` | **Atributo de estado**, regido por la tabla de abajo |
| `SolicitadoEn` | `DateTimeOffset` (UTC) | Instante de la solicitud |
| `EntregadoEn` | `DateTimeOffset?` (UTC) | Instante de la entrega del ejemplar |
| `ResueltoEn` | `DateTimeOffset?` (UTC) | Instante en que llega a un estado terminal |
| `FechaLimite` | `DateTimeOffset?` (UTC) | Fecha límite de devolución acordada |
| `Motivo` | `string?` | Motivo cuando se rechaza o se declara perdido |

En SQL Server el estado se persiste como `NVARCHAR(20)` con una restricción `CHECK`, no
como entero: así el estado es legible directamente en SSMS y en los reportes de RF-REP-03.

---

## 2. Estados (RF-NEG-03)

Declarados **en un solo lugar del código**: `EstadosPrestamo` (la tabla canónica) y el
enum `EstadoPrestamo` (la representación en C#). Son cinco, dentro del rango de 3 a 5 que
exige el requisito.

| Estado | Significado | Terminal |
|---|---|---|
| `Solicitado` | El socio lo pidió y espera respuesta. **Estado inicial.** | No |
| `Activo` | El ejemplar fue entregado al socio. | No |
| `Devuelto` | El ejemplar volvió a la biblioteca. | **Sí** |
| `Rechazado` | Se denegó la petición. | **Sí** |
| `Perdido` | El ejemplar se declaró extraviado. | **Sí** |

**Estado inicial:** `Solicitado`.
**Estados terminales (RF-NEG-05):** `Devuelto`, `Rechazado`, `Perdido` — tres, y de cada
uno no sale ninguna transición.

---

## 3. Transiciones permitidas (RF-NEG-04)

| Desde | Hacia | Quién | Condición |
|---|---|---|---|
| `Solicitado` | `Activo` | Administrador | Hay al menos un ejemplar disponible del título y el socio no tiene bloqueos ni deuda. |
| `Solicitado` | `Rechazado` | Administrador | No hay ejemplares disponibles, o el socio tiene deuda pendiente o préstamos vencidos. |
| `Activo` | `Devuelto` | Estándar (dueño) | Se registra la devolución del ejemplar y queda disponible para otro socio. |
| `Activo` | `Perdido` | Administrador | El ejemplar se declara extraviado y el socio queda bloqueado hasta regularizar. |

Cualquier combinación que no esté en esta tabla **se rechaza**. No hay transición
«por defecto»: la lista es exhaustiva.

---

## 4. Transiciones prohibidas (RF-NEG-04)

Declaradas explícitamente, no sólo ausentes de la tabla anterior. Un rechazo que no se
puede explicar no es defendible en la revisión.

| Desde | Hacia | Por qué se prohíbe |
|---|---|---|
| `Solicitado` | `Devuelto` | No se puede devolver un préstamo que nunca llegó a entregarse. |
| `Devuelto` | `Activo` | Un préstamo devuelto es terminal: el ejemplar se presta de nuevo creando **otro** préstamo. |
| `Rechazado` | `Activo` | Un rechazo no se revierte; el socio debe crear una solicitud nueva. |
| `Perdido` | `Activo` | Un préstamo perdido es terminal; sólo se cierra con el pago de la reposición. |

Las tres últimas se siguen además de la regla general de terminalidad (RF-NEG-05). Se
declaran una a una para que quede escrito **por qué** cada una se rechaza.

---

## 5. Diagrama

```
                    ┌──────────────┐
      (solicita) ──▶│  Solicitado  │
                    └──────┬───────┘
                           │
              ┌────────────┴────────────┐
              │                         │
      (hay ejemplar)          (no hay ejemplar)
              │                         │
              ▼                         ▼
       ┌─────────────┐          ┌─────────────┐
       │    Activo   │          │  Rechazado  │  ✖
       └──────┬──────┘          └─────────────┘  terminal
              │
    ┌─────────┴──────────┐
    │                    │
(devuelve)        (se extravía)
    │                    │
    ▼                    ▼
┌───────────┐      ┌───────────┐
│ Devuelto  │      │  Perdido  │  ✖
│  ✖        │      │  ✖        │  terminal
└───────────┘      └───────────┘
terminal

✖ = estado terminal: de aquí no sale ninguna transición.
```

---

## 6. Dónde vive esto en el código

| Elemento | Archivo |
|---|---|
| Estados canónicos | `src/Biblioteca.Biblioteca/Prestamos/EstadosPrestamo.cs` |
| Enum | `src/Biblioteca.Biblioteca/Prestamos/EstadoPrestamo.cs` |
| Transiciones permitidas y prohibidas | `src/Biblioteca.Biblioteca/Prestamos/TransicionesPrestamo.cs` |
| Entidad `Prestamo` | `src/Biblioteca.Biblioteca/Prestamos/Prestamo.cs` |
| Restricción `CHECK` en SQL Server | `src/Biblioteca.Biblioteca/Persistencia/BibliotecaDbContext.cs` |
| Pruebas de la estructura | `tests/Biblioteca.Biblioteca.Tests/MaquinaDeEstadosDelNegocioTests.cs` |

---

## 7. Independencia respecto a la máquina del Core (RF-NEG-09)

Esta máquina **no** es la de Gestión de permisos. Son dos máquinas distintas, en dos
proyectos distintos, y sus pruebas se mantienen separadas:

- Cambiar los estados de permisos (pieza 2, semanas 6-8) **no obliga** a tocar estos.
- Cambiar estas transiciones **no obliga** a tocar la de permisos.
- No hay ningún archivo compartido entre las dos declaraciones.

---

## 8. Cómo se verifica

```powershell
dotnet test tests/Biblioteca.Biblioteca.Tests
```

Las 19 pruebas comprueban, entre otras cosas:

- que hay entre 3 y 5 estados y que están declarados en un solo lugar;
- que hay exactamente 3 estados terminales y que de ninguno sale ninguna transición;
- que toda transición permitida sale de un estado no terminal;
- que ninguna transición aparece a la vez como permitida y como prohibida;
- que `Solicitado → Devuelto` está rechazada de forma explícita;
- que el texto persistido en SQL Server va y vuelve sin perder el estado.