using Biblioteca.Correo;
using Biblioteca.Correo.Entidades;
using Biblioteca.Correo.Persistencia;
using Biblioteca.Nucleo.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biblioteca.Correo.Enviador;

/// <summary>
/// Proceso independiente que envía los correos encolados (RF-NOT-09).
///
/// Es un ejecutable aparte, no un hilo de la API, precisamente para que:
///
///  1. El envío ocurra FUERA del flujo que creó el correo.
///  2. Se pueda ejecutar sin que ese flujo vuelva a correr.
///  3. Registrar un usuario NO dependa de que el servidor SMTP responda (RF-NOT-08).
///
/// Uso:  dotnet run --project tools/Biblioteca.Correo.Enviador
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddEnvironmentVariables();

        var conexion = builder.Configuration.GetConnectionString("Biblioteca")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'Biblioteca'. Defina la variable de entorno " +
                "ConnectionStrings__Biblioteca (RD-10).");

        builder.Services.Configure<OpcionesCorreo>(
            builder.Configuration.GetSection(OpcionesCorreo.Seccion));

        builder.Services.AddDbContextFactory<CorreoDbContext>(o => o.UseSqlServer(conexion));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<SmtpEntregador>();

        using var servicios = builder.Services.BuildServiceProvider();
        await using var servicio = servicios.CreateAsyncScope();

        var logger = servicio.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Biblioteca.Correo.Enviador");

        var fabrica = servicio.ServiceProvider.GetRequiredService<IDbContextFactory<CorreoDbContext>>();
        var entregador = servicio.ServiceProvider.GetRequiredService<SmtpEntregador>();

        await using var db = await fabrica.CreateDbContextAsync();

        // Sólo se miran los pendientes. Un correo ya reclamado por otro proceso no
        // aparece aquí, y un correo Enviado jamás se vuelve a enviar (RF-NOT-12).
        var pendientes = await db.CorreosEnCola
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCorreo.Pendiente)
            .OrderBy(c => c.CreadoEn)
            .Select(c => c.Id)
            .ToListAsync();

        if (pendientes.Count == 0)
        {
            logger.LogInformation("No hay correos pendientes.");
            return 0;
        }

        logger.LogInformation("Correos pendientes: {Cantidad}", pendientes.Count);

        var enviados = 0;
        var fallidos = 0;

        foreach (var id in pendientes)
        {
            try
            {
                if (await entregador.EntregarAsync(fabrica, id))
                {
                    enviados++;
                    logger.LogInformation("Correo {Id} entregado.", id);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fallidos++;
                logger.LogWarning("Correo {Id} no entregado: {Motivo}", id, ex.Message);
            }
        }

        logger.LogInformation("Proceso terminado. Enviados: {Enviados}. Con fallo: {Fallidos}", enviados, fallidos);
        return fallidos == 0 ? 0 : 1;
    }
}