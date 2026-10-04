namespace Biblioteca.Biblioteca.Prestamos;

/// <summary>
/// ÚNICA declaración de los estados del módulo de negocio (RF-NEG-03).
/// Todos en un solo lugar del código, como exige el requisito.
/// </summary>
public static class EstadosPrestamo
{
    /// <summary>Todos los estados, en el orden de la máquina.</summary>
    public static readonly IReadOnlyList<EstadoPrestamo> Todos =
    [
        EstadoPrestamo.Solicitado,
        EstadoPrestamo.Activo,
        EstadoPrestamo.Devuelto,
        EstadoPrestamo.Rechazado,
        EstadoPrestamo.Perdido
    ];

    /// <summary>Estados de los que no sale ninguna transición (RF-NEG-05).</summary>
    public static readonly IReadOnlySet<EstadoPrestamo> Terminales = new HashSet<EstadoPrestamo>
    {
        EstadoPrestamo.Devuelto,
        EstadoPrestamo.Rechazado,
        EstadoPrestamo.Perdido
    };

    /// <summary>Estado en que nace todo préstamo.</summary>
    public const EstadoPrestamo Inicial = EstadoPrestamo.Solicitado;

    /// <summary>Nombre legible que se persiste en SQL Server y aparece en la documentación.</summary>
    public static string Nombre(EstadoPrestamo estado) => estado.ToString();

    /// <summary>Convierte desde el texto persistido. Lanza si el valor no es un estado válido.</summary>
    public static EstadoPrestamo DesdeTexto(string texto) =>
        Enum.TryParse<EstadoPrestamo>(texto, ignoreCase: false, out var estado)
            ? estado
            : throw new InvalidOperationException($"'{texto}' no es un estado de préstamo válido.");

    public static bool EsTerminal(EstadoPrestamo estado) => Terminales.Contains(estado);
}