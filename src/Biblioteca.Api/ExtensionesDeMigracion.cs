using Biblioteca.Biblioteca.Persistencia;
using Biblioteca.Correo.Persistencia;
using Biblioteca.Identidad.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Api;

/// <summary>Aplica las migraciones de las tres piezas al arrancar la API.</summary>
public static class ExtensionesDeMigracion
{
    public static async Task AplicarMigracionesAsync(IServiceProvider servicios)
    {
        await using var servicio = servicios.CreateAsyncScope();

        // Cada contexto es de una pieza distinta, con su propia carpeta de migraciones.
        // Se aplican en orden para que las claves foráneas entre piezas tengan sentido.
        var identidad = servicio.ServiceProvider.GetRequiredService<IdentidadDbContext>();
        await identidad.Database.MigrateAsync();

        var correo = servicio.ServiceProvider.GetRequiredService<CorreoDbContext>();
        await correo.Database.MigrateAsync();

        var biblioteca = servicio.ServiceProvider.GetRequiredService<BibliotecaDbContext>();
        await biblioteca.Database.MigrateAsync();
    }
}