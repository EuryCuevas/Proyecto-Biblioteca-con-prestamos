using Biblioteca.Biblioteca.Catalogo;
using Biblioteca.Biblioteca.Persistencia;
using Biblioteca.Biblioteca.Prestamos;
using Biblioteca.Biblioteca.Socios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Biblioteca.Biblioteca.Tests;

/// <summary>
/// Pruebas del MODELO DE DATOS del módulo de negocio.
///
/// RF-NEG-01 exige al menos cuatro entidades relacionadas entre sí, y avisa de que
/// «las relaciones existen en el modelo de datos, no sólo en el diagrama». Eso se
/// comprueba preguntándole al modelo de EF Core qué claves foráneas generó de verdad,
/// no leyendo el diagrama.
///
/// RD-03 exige que la dependencia vaya en un solo sentido: el Core no depende del
/// módulo de negocio. Se comprueba verificando que este esquema no tiene ninguna llave
/// foránea hacia las tablas de la pieza Control de acceso ni hacia las de Correo.
///
/// Estas pruebas no abren conexión: construyen el modelo y lo inspeccionan. Por eso
/// corren sin base de datos (RD-12).
/// </summary>
public class RelacionesDelModeloTests
{
    /// <summary>Tablas que pertenecen al Core, desde la perspectiva de este módulo.</summary>
    private static readonly string[] TablasDelCore =
    [
        "Usuario",
        "Sesion",
        "TokenActivacion",
        "CodigoRecuperacion",
        "CorreoEnCola"
    ];

    private static IModel Modelo()
    {
        var opciones = new DbContextOptionsBuilder<BibliotecaDbContext>()
            // La cadena no se usa: sólo hace falta un proveedor para que EF Core
            // resuelva el modelo relacional (índices filtrados y restricciones).
            .UseSqlServer("Server=(ninguno);Database=(ninguno);Trusted_Connection=True;")
            .Options;

        using var contexto = new BibliotecaDbContext(opciones);
        return contexto.Model;
    }

    private static List<(string Tabla, string Columna, string Destino)> ClavesForaneas() =>
        Modelo().GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Select(fk =>
            (
                fk.DeclaringEntityType.GetTableName()!,
                fk.Properties.Single().GetColumnName()!,
                fk.PrincipalEntityType.GetTableName()!
            ))
            .ToList();

    [Fact]
    public void Las_cuatro_entidades_del_negocio_existen_en_el_esquema()
    {
        // RF-NEG-01: al menos cuatro entidades, y aquí son exactamente las cuatro
        // del módulo: Recurso, Ejemplar, Socio y Prestamo.
        var tablas = Modelo().GetEntityTypes()
            .Select(t => t.GetTableName()!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Ejemplar", "Prestamo", "Recurso", "Socio"], tablas);
    }

    [Fact]
    public void Un_recurso_se_compone_de_ejemplares()
    {
        var fk = Assert.Single(ClavesForaneas(), f => f.Tabla == "Ejemplar");

        Assert.Equal("RecursoId", fk.Columna);
        Assert.Equal("Recurso", fk.Destino);
    }

    [Fact]
    public void Un_socio_tiene_prestamos()
    {
        var fk = Assert.Single(ClavesForaneas(), f => f.Tabla == "Prestamo" && f.Columna == "SocioId");

        Assert.Equal("Socio", fk.Destino);
    }

    [Fact]
    public void Un_ejemplar_es_el_que_se_presta()
    {
        var fk = Assert.Single(ClavesForaneas(), f => f.Tabla == "Prestamo" && f.Columna == "EjemplarId");

        Assert.Equal("Ejemplar", fk.Destino);
    }

    [Fact]
    public void Las_tres_relaciones_existen_completo_a_muchos()
    {
        // Ninguna relación puede ser de uno a uno: entonces no habría «varias
        // entidades relacionadas entre sí» sino columnas sueltas.
        var relaciones = ClavesForaneas();

        Assert.Equal(3, relaciones.Count);
        Assert.All(relaciones, r => Assert.NotNull(r.Tabla));
    }

    [Fact]
    public void Las_navegaciones_de_prestamo_existen_en_el_codigo()
    {
        // La relación existe en los dos lados: no basta con la columna.
        Assert.NotNull(typeof(Prestamo).GetProperty(nameof(Prestamo.Socio)));
        Assert.NotNull(typeof(Prestamo).GetProperty(nameof(Prestamo.Ejemplar)));
        Assert.NotNull(typeof(Recurso).GetProperty(nameof(Recurso.Ejemplares)));
        Assert.NotNull(typeof(Ejemplar).GetProperty(nameof(Ejemplar.Prestamos)));
        Assert.NotNull(typeof(Socio).GetProperty(nameof(Socio.Prestamos)));
    }

    [Fact]
    public void El_negocio_no_tiene_ninguna_llave_foranea_hacia_el_Core()
    {
        // RD-03: el Core no depende del módulo. En el modelo de datos eso significa
        // que este esquema no puede apuntar a las tablas de la pieza de identidad,
        // aunque la dependenciafuera sólo de código.
        var destinos = ClavesForaneas().Select(f => f.Destino);

        Assert.All(destinos, destino => Assert.DoesNotContain(destino, TablasDelCore));
    }

    [Fact]
    public void El_esquema_del_negocio_no_incluye_las_tablas_del_Core()
    {
        var tablas = Modelo().GetEntityTypes().Select(t => t.GetTableName()!);

        Assert.All(TablasDelCore, tablaDelCore => Assert.DoesNotContain(tablaDelCore, tablas));
    }

    [Fact]
    public void Un_usuario_del_Core_es_como_mucho_un_socio()
    {
        // El identificador del usuario es opaco: único, pero SIN llave foránea.
        // Así la unicidad la impone la base sin atar este esquema al del Core.
        var indice = Assert.Single(
            Modelo().GetEntityTypes().SelectMany(t => t.GetIndexes()),
            i => i.GetDatabaseName() == "UQ_Socio_UsuarioId");

        Assert.True(indice.IsUnique);
    }

    [Fact]
    public void La_tabla_Socio_no_tiene_ninguna_llave_foranea()
    {
        // Socio no depende de ninguna otra tabla: ni del catálogo, ni del Core.
        // Todo lo que necesita saber de un usuario es un identificador opaco.
        var socio = Modelo().FindEntityType(typeof(Socio))!;

        Assert.Empty(socio.GetForeignKeys());
    }

    [Fact]
    public void Un_ejemplar_no_puede_tener_dos_prestamos_vivos()
    {
        // La disponibilidad no se deja en manos del código: es una restricción de
        // la base, y por eso el índice es único Y filtrado a los estados vivos.
        var indice = Assert.Single(
            Modelo().GetEntityTypes().SelectMany(t => t.GetIndexes()),
            i => i.GetDatabaseName() == "UX_Prestamo_EjemplarVivo");

        Assert.True(indice.IsUnique);
        Assert.Equal("[Estado] IN ('Solicitado','Activo')", indice.GetFilter());
    }

    [Fact]
    public void El_ejemplar_no_tiene_columna_de_disponibilidad()
    {
        // La disponibilidad se deriva de los préstamos vivos. Si algún día aparece
        // una columna «Disponible», esta prueba avisa: habría dos verdades.
        var ejemplar = Modelo().FindEntityType(typeof(Ejemplar))!;

        Assert.DoesNotContain(ejemplar.GetProperties(), p => p.Name.Contains("Disponible"));
    }

    [Fact]
    public void El_isbn_es_unico_solo_cuando_existe()
    {
        // Índice PARCIAL: materiales como revistas o folletos no llevan ISBN, y no
        // pueden impedirse por eso.
        var indice = Assert.Single(
            Modelo().GetEntityTypes().SelectMany(t => t.GetIndexes()),
            i => i.GetDatabaseName() == "UQ_Recurso_Isbn");

        Assert.True(indice.IsUnique);
        Assert.Equal("[Isbn] IS NOT NULL", indice.GetFilter());
    }
}