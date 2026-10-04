namespace Biblioteca.Identidad.Entidades;

/// <summary>
/// Código de recuperación de contraseña de un solo uso (RF-CA-10).
/// Conserva los atributos que el Core exige para la entidad <c>CodigoRecuperacion</c>:
/// usuario, código, fecha de emisión, fecha de vencimiento y usado/no usado.
/// </summary>
public sealed class CodigoRecuperacion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>
    /// Hash del código que viaja en el correo. En la base no hay ningún valor
    /// utilizable (RF-AUD-05 impide además que el código entre en auditoría).
    /// </summary>
    public required byte[] CodigoHash { get; set; }

    /// <summary>Fecha de emisión del código.</summary>
    public DateTimeOffset EmitidoEn { get; set; }

    /// <summary>Fecha de vencimiento. Usado dos veces o vencido, se rechaza (RF-CA-10).</summary>
    public DateTimeOffset VenceEn { get; set; }

    /// <summary>Instante de uso, o <see langword="null"/> si sigue sin usar.</summary>
    public DateTimeOffset? UsadoEn { get; set; }

    public bool EstaVigente(DateTimeOffset ahora) => UsadoEn is null && VenceEn > ahora;
}