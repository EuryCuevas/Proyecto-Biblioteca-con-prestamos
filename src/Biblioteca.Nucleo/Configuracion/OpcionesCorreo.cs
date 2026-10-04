namespace Biblioteca.Nucleo.Configuracion;

/// <summary>
/// Credenciales y datos del servidor de correo.
///
/// RD-10 / RF-NOT-13: estos valores NUNCA se escriben en el repositorio. Se
/// inyectan desde variables de entorno con el prefijo <c>Correo__</c>, por ejemplo
/// <c>Correo__Host</c>, <c>Correo__Puerto</c>, <c>Correo__Usuario</c>,
/// <c>Correo__Contrasena</c>, <c>Correo__UsarSsl</c> y <c>Correo__Remitente</c>.
/// En <c>appsettings.json</c> sólo viven valores por defecto sin secreto.
/// </summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    public string Host { get; init; } = string.Empty;

    public int Puerto { get; init; } = 587;

    public string Usuario { get; init; } = string.Empty;

    public string Contrasena { get; init; } = string.Empty;

    public bool UsarSsl { get; init; } = true;

    public string Remitente { get; init; } = string.Empty;

    /// <summary>Longitud en minutos de la validez de un enlace o código de un solo uso.</summary>
    public int MinutosValidezToken { get; init; } = 30;

    /// <summary>Minutos de bloqueo tras agotar los intentos fallidos (RF-CA-19).</summary>
    public int MinutosBloqueo { get; init; } = 15;

    /// <summary>Intentos fallidos consecutivos que disparan el bloqueo (RF-CA-19).</summary>
    public int IntentosMaximos { get; init; } = 5;

    /// <summary>
    /// Horas de validez de una credencial de sesión. Ningún requisito las fija;
    /// se declara aquí para que la decisión quede explícita y sea ajustable.
    /// </summary>
    public int HorasValidezSesion { get; init; } = 8;
}