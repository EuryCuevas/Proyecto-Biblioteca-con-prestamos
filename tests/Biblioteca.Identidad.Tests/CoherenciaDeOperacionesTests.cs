using Biblioteca.Identidad.Autorizacion;

namespace Biblioteca.Nucleo.Tests;

/// <summary>
/// Comprueba que el guard de RF-CA-05 realmente detiene el arranque de la aplicación.
/// Sin esta prueba, la convención sería una simple convención; con ella, el requisito
/// se vuelve algo comprobable sin necesidad de levantar la API.
/// </summary>
public class CoherenciaDeOperacionesTests
{
    [Fact]
    public void Una_accion_sin_operacion_declarada_es_rechazada()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() =>
            CoherenciaDeOperaciones.Validar("UsuariosController.Listar", operacion: null));

        Assert.Contains("RF-CA-05", excepcion.Message);
    }

    [Fact]
    public void Una_accion_con_operacion_vacia_es_rechazada()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CoherenciaDeOperaciones.Validar("UsuariosController.Listar", operacion: "   "));
    }

    [Fact]
    public void Una_accion_con_operacion_inexistente_es_rechazada()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() =>
            CoherenciaDeOperaciones.Validar("UsuariosController.Listar", "usuarios.inventada"));

        Assert.Contains("PoliticaDeOperaciones", excepcion.Message);
    }

    [Fact]
    public void Una_accion_con_operacion_valida_pasa_sin_error()
    {
        CoherenciaDeOperaciones.Validar(
            "UsuariosController.Listar", "usuarios.listar");
    }
}