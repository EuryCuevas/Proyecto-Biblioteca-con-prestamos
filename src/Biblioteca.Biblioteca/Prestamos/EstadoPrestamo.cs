namespace Biblioteca.Biblioteca.Prestamos;

/// <summary>
/// Estados del préstamo, la entidad central del módulo de negocio.
/// RF-NEG-03 exige entre 3 y 5 estados; aquí hay 5, tres de ellos terminales
/// (RF-NEG-05). El enum no es la declaración canónica: la canónica es
/// <see cref="EstadosPrestamo"/>, para que el texto que se persiste en SQL Server
/// y el que aparece en la documentación no puedan divergir.
/// </summary>
public enum EstadoPrestamo
{
    /// <summary>El socio lo pidió y espera respuesta. Estado inicial.</summary>
    Solicitado = 0,

    /// <summary>El ejemplar fue entregado al socio. Estado activo.</summary>
    Activo = 1,

    /// <summary>El ejemplar volvió a la biblioteca. Estado terminal.</summary>
    Devuelto = 2,

    /// <summary>Se denegó la petición. Estado terminal.</summary>
    Rechazado = 3,

    /// <summary>El ejemplar se declaró extraviado. Estado terminal.</summary>
    Perdido = 4
}