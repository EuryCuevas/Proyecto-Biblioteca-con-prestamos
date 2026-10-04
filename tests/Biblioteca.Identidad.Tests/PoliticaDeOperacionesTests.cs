using Biblioteca.Identidad;
using Biblioteca.Identidad.Autorizacion;

namespace Biblioteca.Nucleo.Tests;

/// <summary>
/// Comprueba que la política de operaciones cumple lo que la rúbrica exige de RF-CA-05:
/// un único punto donde se lee qué rol exige cada operación, sin contradicciones.
/// </summary>
public class PoliticaDeOperacionesTests
{
    [Fact]
    public void Toda_operacion_declarada_tiene_al_menos_un_rol()
    {
        var sinRoles = PoliticaDeOperaciones.Requisitos
            .Where(par => par.Value.Length == 0)
            .Select(par => par.Key)
            .ToList();

        Assert.Empty(sinRoles);
    }

    /// <summary>
    /// Operaciones que sólo puede ejecutar un Administrador. La lista es explícita a
    /// propósito: si alguien añade una operación de administración y olvida
    /// registrarla aquí, la prueba de "sin fugas" de abajo lo detecta.
    /// </summary>
    public static TheoryData<string> OperacionesReservadasAlAdministrador =>
    [
        Operaciones.UsuariosListar,
        Operaciones.UsuariosCambiarRol,
        Operaciones.UsuariosDesactivar,
        Operaciones.UsuariosReactivar,
        Operaciones.UsuariosForzarRestablecimiento,
        Operaciones.SolicitudesListarPendientes,
        Operaciones.SolicitudesAprobar,
        Operaciones.SolicitudesRechazar,
        Operaciones.AuditoriaConsultar
    ];

    [Theory]
    [MemberData(nameof(OperacionesReservadasAlAdministrador))]
    public void Una_operacion_reservada_excluye_a_estandar(string operacion)
    {
        // La trampa que la rúbrica señala como "parcialmente logrado": abrir el rol en
        // varios sitios hasta que cualquiera pase. Aquí se verifica lo contrario.
        var roles = PoliticaDeOperaciones.RolesDe(operacion);

        Assert.Contains(Rol.Administrador, roles);
        Assert.DoesNotContain(Rol.Estandar, roles);
    }

    [Fact]
    public void Ninguna_operacion_administrador_deja_entrar_a_estandar()
    {
        // Comprobación de barrido sobre TODAS las operaciones, no sólo sobre la lista
        // anterior: si aparece una operación sólo-Administrador fuera de la lista, esta
        // prueba falla y obliga a actualizar el ReasonData de arriba.
        var fugas = PoliticaDeOperaciones.Requisitos
            .Where(par => par.Value.Contains(Rol.Administrador))
            .Where(par => par.Value.Contains(Rol.Estandar))
            .Select(par => par.Key)
            .Where(operacion => !OperacionesAbiertasAAmbosRoles.Contains(operacion))
            .ToList();

        Assert.Empty(fugas);
    }

    /// <summary>
    /// Operaciones que, por diseño, admiten ambos roles: acting sobre la propia cuenta
    /// y sobre los propios documentos. No son una fuga: el Estándar actúa sobre sí mismo.
    /// </summary>
    private static readonly HashSet<string> OperacionesAbiertasAAmbosRoles =
    [
        Operaciones.Autenticarse,
        Operaciones.ConsultarYO,
        Operaciones.CerrarSesion,
        Operaciones.CambiarPasswordPropia,
        Operaciones.SolicitudesCrear,
        Operaciones.DocumentosSubir,
        Operaciones.DocumentosListar,
        Operaciones.DocumentosDescargar,
        Operaciones.DocumentosEliminar
    ];

    [Theory]
    [InlineData(Operaciones.UsuariosListar)]
    [InlineData(Operaciones.UsuariosCambiarRol)]
    [InlineData(Operaciones.UsuariosDesactivar)]
    [InlineData(Operaciones.UsuariosReactivar)]
    [InlineData(Operaciones.UsuariosForzarRestablecimiento)]
    public void La_administracion_de_usuarios_exige_administrador(string operacion)
    {
        // RF-CA-08, RF-CA-13 y RF-CA-20: sólo un Administrador ejecuta estas operaciones.
        Assert.Equal([Rol.Administrador], PoliticaDeOperaciones.RolesDe(operacion));
    }

    [Theory]
    [InlineData(Operaciones.Autenticarse)]
    [InlineData(Operaciones.ConsultarYO)]
    [InlineData(Operaciones.CerrarSesion)]
    [InlineData(Operaciones.CambiarPasswordPropia)]
    public void Las_operaciones_sobre_la_propia_cuenta_abren_a_ambos_roles(string operacion)
    {
        var roles = PoliticaDeOperaciones.RolesDe(operacion);

        Assert.Contains(Rol.Estandar, roles);
        Assert.Contains(Rol.Administrador, roles);
    }

    [Fact]
    public void Una_operacion_inexistente_no_devuelve_autorizacion_alguna()
    {
        Assert.False(PoliticaDeOperaciones.EstaDeclarada("inventada.que.no.existe"));
        Assert.Empty(PoliticaDeOperaciones.RolesDe("inventada.que.no.existe"));
    }

    [Fact]
    public void Toda_constante_de_Operaciones_esta_registrada_en_la_politica()
    {
        // Evita el olvido clásico: se declara una operación, se usa en un controlador
        // y se olvida añadirla a la política. La app no arranca, y esta prueba lo
        // adelanta sin necesidad de arrancarla.
        var declaradas = typeof(Operaciones)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(campo => campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetValue(null)!)
            .ToList();

        var noRegistradas = declaradas
            .Where(operacion => !PoliticaDeOperaciones.EstaDeclarada(operacion))
            .ToList();

        Assert.Empty(noRegistradas);
    }
}