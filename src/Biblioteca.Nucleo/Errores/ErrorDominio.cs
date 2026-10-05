namespace Biblioteca.Nucleo.Errores;

/// <summary>
/// Clasificación de un rechazo controlado. La capa de presentación traduce estos
/// tipos a respuestas HTTP sin exponer trazas, rutas ni consultas (RD-08).
/// </summary>
public enum TipoError
{
    /// <summary>Entrada ausente, mal formada o fuera de rango (RD-07).</summary>
    Validacion,

    /// <summary>
    /// La identidad no quedó establecida: credenciales que no sirven, cuenta bloqueada
    /// por intentos o cuenta sin activar. Se traduce a <b>401</b>, que significa
    /// "no sé quién eres" (RF-CA-03, RF-CA-15, RF-CA-19).
    /// </summary>
    NoAutenticado,

    /// <summary>
    /// La identidad se conoce pero la operación está reservada a otro rol. Se traduce a
    /// <b>403</b>: "sé quién eres, pero no puedes" (RF-CA-05, RF-CA-06, RD-06).
    /// </summary>
    NoAutorizado,

    /// <summary>El recurso solicitado no existe.</summary>
    NoEncontrado,

    /// <summary>Conflicto con el estado actual: correo duplicado, token usado, etc.</summary>
    Conflicto,

    /// <summary>Una regla de negocio se opone: transición prohibida, usuario inactivo, etc.</summary>
    ReglaDeNegocio,

    /// <summary>Fallo no controlado. El mensaje visible al usuario es genérico.</summary>
    Interno
}

/// <summary>
/// Excepción de dominio: el rechazo esperado de una regla de negocio.
/// No transporta datos sensibles al usuario; <see cref="Mensaje"/> es el texto
/// que sí se puede mostrar (RD-08).
/// </summary>
public sealed class ExcepcionDominio : Exception
{
    public ExcepcionDominio(TipoError tipo, string codigo, string mensaje, Exception? causa = null)
        : base(mensaje, causa)
    {
        Tipo = tipo;
        Codigo = codigo;
    }

    public TipoError Tipo { get; }

    /// <summary>Código estable, útil para el cliente y para trazar sin filtrar detalles.</summary>
    public string Codigo { get; }

    public static ExcepcionDominio Validacion(string codigo, string mensaje) =>
        new(TipoError.Validacion, codigo, mensaje);

    public static ExcepcionDominio NoAutenticado(string codigo, string mensaje) =>
        new(TipoError.NoAutenticado, codigo, mensaje);

    public static ExcepcionDominio NoAutorizado(string codigo, string mensaje) =>
        new(TipoError.NoAutorizado, codigo, mensaje);

    public static ExcepcionDominio NoEncontrado(string codigo, string mensaje) =>
        new(TipoError.NoEncontrado, codigo, mensaje);

    public static ExcepcionDominio Conflicto(string codigo, string mensaje) =>
        new(TipoError.Conflicto, codigo, mensaje);

    public static ExcepcionDominio ReglaDeNegocio(string codigo, string mensaje) =>
        new(TipoError.ReglaDeNegocio, codigo, mensaje);
}