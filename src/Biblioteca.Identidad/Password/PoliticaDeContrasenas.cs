using System.Security.Cryptography;
using System.Text;

namespace Biblioteca.Identidad.Password;

/// <summary>
/// Política mínima de contraseña exigida por RF-CA-14: al menos 8 caracteres, con
/// letras y números.
///
/// Es una función pura, sin dependencias de base de datos ni de ASP.NET, para poder
/// probarse directamente (RD-12). Se aplica en el registro y en TODOS los cambios
/// de contraseña: con la actual (RF-CA-22), por recuperación (RF-CA-11) y por
/// restablecimiento forzado (RF-CA-13).
/// </summary>
public static class PoliticaDeContrasenas
{
    public const int LongitudMinima = 8;

    /// <summary>
    /// Valida la contraseña y devuelve el motivo del rechazo, o <see langword="null"/>
    /// si cumple la política. El mensaje está pensado para mostrarse al usuario:
    /// no revela nada del almacenamiento (RD-08).
    /// </summary>
    public static string? Validar(string? contrasena)
    {
        if (string.IsNullOrWhiteSpace(contrasena))
        {
            return "La contraseña es obligatoria.";
        }

        if (contrasena.Length < LongitudMinima)
        {
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        }

        var tieneLetra = contrasena.Any(char.IsLetter);
        var tieneNumero = contrasena.Any(char.IsDigit);

        if (!tieneLetra || !tieneNumero)
        {
            return "La contraseña debe combinar letras y números.";
        }

        return null;
    }

    /// <summary>Indica si la contraseña cumple la política.</summary>
    public static bool Cumple(string? contrasena) => Validar(contrasena) is null;
}

/// <summary>
/// Genera tokens y códigos de un solo uso, y su hash para persistirlo.
/// 256 bits de entropía. Nunca se persiste el valor en claro (RF-CA-15, RF-CA-10).
/// </summary>
public static class GeneradorDeSecretos
{
    /// <summary>Devuelve el valor en claro que viajará al usuario dentro del enlace o correo.</summary>
    public static string NuevoToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>SHA-256 del valor en claro: es lo único que se guarda en la base.</summary>
    public static byte[] Hash(string valor) => SHA256.HashData(Encoding.UTF8.GetBytes(valor));

    /// <summary>Compara en tiempo constante para no filtrar información por temporización.</summary>
    public static bool Coincide(byte[] hashEsperado, byte[] hashRecibido) =>
        CryptographicOperations.FixedTimeEquals(hashEsperado, hashRecibido);
}