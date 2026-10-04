using System.ComponentModel.DataAnnotations;
using Biblioteca.Identidad;

namespace Biblioteca.Identidad.Entidades;

/// <summary>
/// Usuario del sistema. Atributos mínimos exigidos por el Core para
/// <c>Usuario</c>, más los que necesitan los criterios de aceptación de esta pieza.
/// </summary>
public sealed class Usuario
{
    /// <summary>Identificador interno.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Correo único (RF-CA-01). La unicidad la garantiza el índice de la base.</summary>
    [Required]
    [MaxLength(256)]
    public string Correo { get; set; } = string.Empty;

    /// <summary>
    /// Hash con sal por usuario. Nunca se expone ni se serializa (RF-CA-02, RF-CA-21).
    /// Dos usuarios con la misma contraseña producen valores distintos.
    /// </summary>
    [Required]
    [MaxLength(400)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Exactamente un rol por usuario (RF-CA-04).</summary>
    public Rol Rol { get; set; } = Rol.Estandar;

    /// <summary>
    /// El usuario nace inactivo y se activa al abrir el enlace recibido por correo
    /// (RF-CA-15). Un Administrador también puede desactivarlo y reactivarlo (RF-CA-20).
    /// </summary>
    public bool Activo { get; set; }

    /// <summary>Intentos fallidos consecutivos; se pone a cero al iniciar sesión bien (RF-CA-19).</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Instante UTC hasta el que la cuenta queda bloqueada (RF-CA-19).</summary>
    public DateTimeOffset? BloqueadoHasta { get; set; }

    /// <summary>Instante UTC de creación. Mismo criterio de reloj en todo el sistema (RD-11).</summary>
    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>
    /// Instante UTC del último cambio de contraseña. Permite invalidar credenciales
    /// de sesión emitidas antes del cambio (RF-CA-12).
    /// </summary>
    public DateTimeOffset? PasswordCambiadoEn { get; set; }

    public ICollection<Sesion> Sesiones { get; set; } = [];
    public ICollection<TokenActivacion> TokensActivacion { get; set; } = [];
    public ICollection<CodigoRecuperacion> CodigosRecuperacion { get; set; } = [];
}