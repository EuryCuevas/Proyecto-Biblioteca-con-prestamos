namespace Biblioteca.Biblioteca.Prestamos;

/// <summary>Quién está autorizado a ejecutar una transición del módulo de negocio.</summary>
public enum EjecutorTransicion
{
    /// <summary>Cualquier usuario dueño del préstamo (socio).</summary>
    Estandar,

    /// <summary>Personal de la biblioteca.</summary>
    Administrador,

    /// <summary>El propio sistema, sin intervención humana.</summary>
    Sistema
}

/// <summary>Una transición permitida de la máquina de estados del negocio.</summary>
/// <param name="Desde">Estado de origen.</param>
/// <param name="Hacia">Estado de destino.</param>
/// <param name="Quien">Quién la ejecuta.</param>
/// <param name="Condicion">Condición que debe cumplirse, en lenguaje de negocio.</param>
public sealed record Transicion(
    EstadoPrestamo Desde,
    EstadoPrestamo Hacia,
    EjecutorTransicion Quien,
    string Condicion);

/// <summary>
/// ÚNICA declaración de las transiciones del módulo de negocio (RD-04).
///
/// La tabla de permisos y la tabla de prohibiciones viven juntas aquí: una
/// transición no listada en <see cref="Permitidas"/> es por definición inválida, y
/// las que están en <see cref="Prohibidas"/> se declaran de forma explícita para que
/// quede documentado el rechazo (RF-NEG-04).
///
/// IMPORTANTE: esta máquina es independiente de la de Gestión de permisos del Core
/// (RF-NEG-09). Cambiar los estados de permisos no obliga a tocar estos, y por eso
/// las pruebas de una y otra se mantienen separadas.
/// </summary>
public static class TransicionesPrestamo
{
    /// <summary>Transiciones permitidas. Cualquier otra combinación se rechaza.</summary>
    public static readonly IReadOnlyList<Transicion> Permitidas =
    [
        new(EstadoPrestamo.Solicitado, EstadoPrestamo.Activo,
            EjecutorTransicion.Administrador,
            "Hay al menos un ejemplar disponible del título y el socio no tiene bloqueos ni deuda."),

        new(EstadoPrestamo.Solicitado, EstadoPrestamo.Rechazado,
            EjecutorTransicion.Administrador,
            "No hay ejemplares disponibles, o el socio tiene deuda pendiente o préstamos vencidos."),

        new(EstadoPrestamo.Activo, EstadoPrestamo.Devuelto,
            EjecutorTransicion.Estandar,
            "Se registra la devolución del ejemplar y queda disponible para otro socio."),

        new(EstadoPrestamo.Activo, EstadoPrestamo.Perdido,
            EjecutorTransicion.Administrador,
            "El ejemplar se declara extraviado y el socio queda bloqueado hasta regularizar.")
    ];

    /// <summary>
    /// Transiciones prohibidas de forma explícita (RF-NEG-04). Intentarlas se rechaza
    /// y el estado no cambia.
    /// </summary>
    public static readonly IReadOnlyList<Transicion> Prohibidas =
    [
        new(EstadoPrestamo.Solicitado, EstadoPrestamo.Devuelto,
            EjecutorTransicion.Administrador,
            "No se puede devolver un préstamo que nunca llegó a entregarse."),

        new(EstadoPrestamo.Devuelto, EstadoPrestamo.Activo,
            EjecutorTransicion.Administrador,
            "Un préstamo devuelto es terminal: el ejemplar se presta de nuevo creando OTRO préstamo."),

        new(EstadoPrestamo.Rechazado, EstadoPrestamo.Activo,
            EjecutorTransicion.Administrador,
            "Un rechazo no se puede revertir; el socio debe crear una solicitud nueva."),

        new(EstadoPrestamo.Perdido, EstadoPrestamo.Activo,
            EjecutorTransicion.Administrador,
            "Un préstamo perdido es terminal; sólo se cierra con el pago de la reposición.")
    ];

    /// <summary>True si la transición está permitida según <see cref="Permitidas"/>.</summary>
    public static bool EsPermitida(EstadoPrestamo desde, EstadoPrestamo hacia) =>
        Permitidas.Any(t => t.Desde == desde && t.Hacia == hacia);

    /// <summary>True si la transición está declarada como prohibida (RF-NEG-04).</summary>
    public static bool EsProhibida(EstadoPrestamo desde, EstadoPrestamo hacia) =>
        Prohibidas.Any(t => t.Desde == desde && t.Hacia == hacia);

    /// <summary>
    /// True si la transición está permitida Y, además, sale de un estado no terminal
    /// (RF-NEG-05). Es la comprobación que usarán las operaciones del módulo.
    /// </summary>
    public static bool PuedeOcurrir(EstadoPrestamo desde, EstadoPrestamo hacia)
    {
        if (EstadosPrestamo.EsTerminal(desde))
        {
            return false;
        }

        return EsPermitida(desde, hacia);
    }

    /// <summary>
    /// Texto de rechazo al intentar una transición no permitida. No revela el
    /// detalle interno de la máquina (RD-08).
    /// </summary>
    public static string MensajeDeRechazo(EstadoPrestamo desde, EstadoPrestamo hacia) =>
        $"La transición de {desde} a {hacia} no está permitida.";

    /// <summary>La tabla completa, para alimentar la documentación y las pruebas.</summary>
    public static IEnumerable<Transicion> Todas => Permitidas.Concat(Prohibidas);
}