namespace Biblioteca.Identidad.Entidades;

/// <summary>
/// Credencial de sesión. El token viaja al cliente; en la base sólo se guarda su
/// hash SHA-256, de modo que leer el almacenamiento no entrega una sesión usable.
///
/// Guardar la sesión en el servidor (en vez de un JWT autocontenido) es lo que
/// permite cumplir los tres requisitos de revocación:
/// RF-CA-12 (cambio de contraseña), RF-CA-18 (cierre de sesión) y
/// RF-CA-20 (desactivación del usuario).
/// </summary>
public sealed class Sesion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>SHA-256 del token opaco. Único e indexado.</summary>
    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ExpiraEn { get; set; }

    /// <summary>Instante UTC en que se cerró o se invalidó. <see langword="null"/> = abierta.</summary>
    public DateTimeOffset? CerradaEn { get; set; }

    /// <summary>Motivo del cierre, útil para trazar sin exponer datos sensibles.</summary>
    public string? MotivoCierre { get; set; }

    public bool EstaAbierta(DateTimeOffset ahora) => CerradaEn is null && ExpiraEn > ahora;
}