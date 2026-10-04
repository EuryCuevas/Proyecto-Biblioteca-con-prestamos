using Microsoft.Extensions.Logging;

namespace Biblioteca.Nucleo.Auditoria;

/// <summary>
/// Implementación temporal del puerto de auditoría: escribe la entrada en el log y no
/// la persiste.
///
/// La Práctica 1 no califica la auditoría —los registros de RF-CA-08, RF-CA-13 y
/// RF-CA-20 llegan con la pieza 6 en la semana 14—, pero el puerto existe desde el
/// primer día para que las piezas publiquen entradas sin acoplarse todavía a la
/// persistencia. En la semana 14 se registra <c>RegistroAuditorias</c> en su lugar y
/// no hay que tocar las piezas.
/// </summary>
public sealed class SumideroDeAuditoria(ILogger<SumideroDeAuditoria> logger) : IPublicaAuditoria
{
    public Task PublicarAsync(EntradaAuditoria entrada, CancellationToken cancelacion = default)
    {
        logger.LogInformation(
            "AUDIT {Accion} {Entidad} {EntidadId} por {Usuario} en {FechaHora} [{Anterior} -> {Nuevo}]",
            entrada.Accion,
            entrada.EntidadAfectada,
            entrada.EntidadId,
            entrada.UsuarioQueActuo,
            entrada.FechaHora,
            entrada.ValorAnterior ?? "-",
            entrada.ValorNuevo ?? "-");

        return Task.CompletedTask;
    }
}