namespace Biblioteca.Correo.Entidades;

/// <summary>Estados de un correo en la cola.</summary>
public enum EstadoCorreo
{
    /// <summary>En cola, esperando a que el proceso emisor lo recoja (RF-NOT-08).</summary>
    Pendiente = 0,

    /// <summary>Reclamado por el proceso emisor. Evita el envío duplicado (RF-NOT-12).</summary>
    Enviando = 1,

    /// <summary>Entregado al servidor de correo (RF-NOT-09).</summary>
    Enviado = 2,

    /// <summary>
    /// Agotados los reintentos. Lo usa la pieza 4 en la semana 11; el andamiaje
    /// de la Práctica 1 no lo necesita, pero el atributo ya está en la entidad.
    /// </summary>
    Fallido = 3
}