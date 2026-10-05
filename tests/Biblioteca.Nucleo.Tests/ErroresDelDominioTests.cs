using Biblioteca.Nucleo.Auditoria;
using Biblioteca.Nucleo.Errores;

namespace Biblioteca.Nucleo.Tests;

/// <summary>
/// El núcleo transversal no tiene lógica de negocio, pero sí contratos que el resto
/// del sistema apoya. Estas pruebas fijan ese contrato (RD-02, RD-08).
/// </summary>
public class ErroresDelDominioTests
{
    [Theory]
    [InlineData(TipoError.Validacion)]
    [InlineData(TipoError.NoAutenticado)]
    [InlineData(TipoError.NoAutorizado)]
    [InlineData(TipoError.NoEncontrado)]
    [InlineData(TipoError.Conflicto)]
    [InlineData(TipoError.ReglaDeNegocio)]
    public void Cada_rechazo_conserva_su_tipo_y_su_codigo(TipoError tipo)
    {
        var excepcion = new ExcepcionDominio(tipo, "prueba.codigo", "Mensaje para el usuario.");

        Assert.Equal(tipo, excepcion.Tipo);
        Assert.Equal("prueba.codigo", excepcion.Codigo);
        Assert.Equal("Mensaje para el usuario.", excepcion.Message);
    }

    [Fact]
    public void Las_ayudas_de_fabrica_no_pierden_la_causa()
    {
        var original = new InvalidOperationException("fallo de origen");
        var envolver = new ExcepcionDominio(
            TipoError.ReglaDeNegocio, "envuelto", "Mensaje para el usuario.", original);

        Assert.Same(original, envolver.InnerException);
    }

    [Fact]
    public void El_mensaje_de_un_rechazo_es_lo_unico_que_sale_al_usuario()
    {
        // El middleware de la API responde con Message y nunca con el tipo de excepción.
        // Esta prueba deja constancia de que Message está pensado para el usuario final.
        var excepcion = ExcepcionDominio.NoAutorizado("acceso.denegado", "No tienes permiso.");

        Assert.Equal("No tienes permiso.", excepcion.Message);
        Assert.DoesNotContain("ExcepcionDominio", excepcion.Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// El puerto de auditoría existe desde el primer día aunque la persistencia llegue en
/// la semana 14 (RF-CA-08, RF-CA-13 y RF-CA-20 no se califican en la Práctica 1).
/// </summary>
public class PuertoDeAuditoriaTests
{
    [Fact]
    public void La_entrada_de_auditoria_conserva_los_atributos_exigidos()
    {
        // RF-AUD-01: quién, qué, sobre qué entidad, cuándo y el estado previo y nuevo.
        var momento = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        var entrada = new EntradaAuditoria(
            UsuarioQueActuo: "3f1b2c44-0000-0000-0000-000000000001",
            Accion: "usuario.rol.cambiado",
            EntidadAfectada: "Usuario",
            EntidadId: "3f1b2c44-0000-0000-0000-000000000002",
            FechaHora: momento,
            ValorAnterior: "Estandar",
            ValorNuevo: "Administrador");

        Assert.Equal("3f1b2c44-0000-0000-0000-000000000001", entrada.UsuarioQueActuo);
        Assert.Equal("usuario.rol.cambiado", entrada.Accion);
        Assert.Equal("Usuario", entrada.EntidadAfectada);
        Assert.Equal("3f1b2c44-0000-0000-0000-000000000002", entrada.EntidadId);
        Assert.Equal(momento, entrada.FechaHora);
        Assert.Equal("Estandar", entrada.ValorAnterior);
        Assert.Equal("Administrador", entrada.ValorNuevo);
    }

    [Fact]
    public void Una_entrada_admite_valores_nulos_para_valor_anterior_y_nuevo()
    {
        // Una creación no tiene estado previo; un alta de sesión tampoco tiene estado nuevo.
        var entrada = new EntradaAuditoria(
            "u1", "usuario.registrado", "Usuario", "u1",
            DateTimeOffset.UnixEpoch, null, null);

        Assert.Null(entrada.ValorAnterior);
        Assert.Null(entrada.ValorNuevo);
    }
}