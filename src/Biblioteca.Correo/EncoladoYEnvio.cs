using System.Net;
using System.Net.Mail;
using Biblioteca.Correo.Entidades;
using Biblioteca.Correo.Persistencia;
using Biblioteca.Nucleo.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biblioteca.Correo;

/// <summary>
/// Encola el correo. Es la única implementación de <see cref="IEncolaCorreo"/> y no
/// toca SMTP en absoluto: escribir la fila ES el envío desde el punto de vista de la
/// operación de negocio (RF-NOT-08).
/// </summary>
public sealed class EncolaCorreo(
    IDbContextFactory<CorreoDbContext> fabrica,
    TimeProvider reloj) : IEncolaCorreo
{
    public async Task EncolarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        string? plantilla = null,
        string? datoPlantilla = null,
        CancellationToken cancelacion = default)
    {
        // Un destinatario vacío o mal formado es un rechazo controlado, nunca una
        // excepción sin manejar (RD-07).
        if (string.IsNullOrWhiteSpace(destinatario) || !MailAddress.TryCreate(destinatario, out var parsed))
        {
            throw new ArgumentException("El destinatario del correo no es válido.", nameof(destinatario));
        }

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        db.CorreosEnCola.Add(new CorreoEnCola
        {
            Destinatario = parsed.Address,
            Asunto = asunto,
            Cuerpo = cuerpo,
            Estado = EstadoCorreo.Pendiente,
            CreadoEn = reloj.GetUtcNow(),
            Plantilla = plantilla,
            DatoPlantilla = datoPlantilla
        });

        await db.SaveChangesAsync(cancelacion);
    }
}

/// <summary>
/// Envía por SMTP un correo ya encolado.
///
/// El cliente SMTP se construye AQUÍ, de forma perezosa, y no en el arranque de la
/// aplicación. Es un requisito explícito: "apagar el acceso al servidor SMTP y
/// registrar un usuario" debe dejar la operación de registro intacta.
/// </summary>
public sealed class SmtpEntregador(IOptions<OpcionesCorreo> opciones)
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    /// <summary>
    /// Reclama el correo con un UPDATE condicional y lo entrega.
    /// Devuelve <see langword="true"/> si este proceso lo envió.
    /// </summary>
    /// <remarks>
    /// RF-NOT-12: el reclamo es un <c>UPDATE ... WHERE Estado = 'Pendiente'</c>. Si
    /// otro proceso ya lo reclamó, la actualización afecta 0 filas y aquí se aborta.
    /// Ejecutar el emisor dos veces seguidas no duplica ningún envío.
    /// </remarks>
    public async Task<bool> EntregarAsync(
        IDbContextFactory<CorreoDbContext> fabrica,
        Guid id,
        CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var afectado = await db.CorreosEnCola
            .Where(c => c.Id == id && c.Estado == EstadoCorreo.Pendiente)
            .ExecuteUpdateAsync(
                cambios => cambios.SetProperty(c => c.Estado, EstadoCorreo.Enviando),
                cancelacion);

        if (afectado == 0)
        {
            // Ya reclamado por otro proceso: no se vuelve a enviar.
            return false;
        }

        var correo = await db.CorreosEnCola.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancelacion);
        if (correo is null)
        {
            return false;
        }

        try
        {
            using var cliente = new SmtpClient(_opciones.Host, _opciones.Puerto)
            {
                EnableSsl = _opciones.UsarSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
            {
                cliente.Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Contrasena);
            }

            using var mensaje = new MailMessage
            {
                From = new MailAddress(_opciones.Remitente),
                Subject = correo.Asunto,
                Body = correo.Cuerpo,
                IsBodyHtml = false
            };
            mensaje.To.Add(correo.Destinatario);

            await cliente.SendMailAsync(mensaje, cancelacion);

            await db.CorreosEnCola
                .Where(c => c.Id == id)
                .ExecuteUpdateAsync(cambios => cambios
                    .SetProperty(c => c.Estado, EstadoCorreo.Enviado)
                    .SetProperty(c => c.FechaEnvio, DateTimeOffset.UtcNow)
                    .SetProperty(c => c.Intentos, c => c.Intentos + 1),
                    cancelacion);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Se registra el fallo y la fila vuelve a Pendiente para que otro intento
            // pueda recogerla. La política de reintentos y el estado Fallido llegan
            // con la pieza 4 en la semana 11 (RF-NOT-10).
            await db.CorreosEnCola
                .Where(c => c.Id == id)
                .ExecuteUpdateAsync(cambios => cambios
                    .SetProperty(c => c.Estado, EstadoCorreo.Pendiente)
                    .SetProperty(c => c.Intentos, c => c.Intentos + 1)
                    .SetProperty(c => c.UltimoError, ex.Message.Length > 400 ? ex.Message[..400] : ex.Message),
                    cancelacion);

            throw;
        }
    }
}