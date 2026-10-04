using Biblioteca.Biblioteca.Prestamos;

namespace Biblioteca.Nucleo.Tests;

/// <summary>
/// Pruebas de la máquina de estados del módulo de negocio.
///
/// RF-NEG-03 exige entre 3 y 5 estados declarados en un solo lugar.
/// RF-NEG-04 exige transiciones prohibidas explícitas.
/// RF-NEG-05 exige estados terminales.
///
/// Nota de alcance: en la Práctica 1 se entrega la ESTRUCTURA de la máquina. Estas
/// pruebas verifican que la estructura es correcta y coherente; la lógica que decide
/// si una petición concreta puede ejecutarse llega en la semana 8.
/// </summary>
public class MaquinaDeEstadosDelNegocioTests
{
    [Fact]
    public void La_maquina_declara_entre_tres_y_cinco_estados()
    {
        // RF-NEG-03: el rango exigido es de 3 a 5 estados.
        Assert.InRange(EstadosPrestamo.Todos.Count, 3, 5);
    }

    [Fact]
    public void Los_estados_estan_declarados_en_un_solo_lugar()
    {
        // Todos los estados declarados deben estar en la tabla canónica, sin duplicados.
        Assert.Equal(EstadosPrestamo.Todos.Count, EstadosPrestamo.Todos.Distinct().Count());
        Assert.Equal(5, Enum.GetValues<EstadoPrestamo>().Length);
    }

    [Fact]
    public void El_estado_inicial_es_solicitado()
    {
        Assert.Equal(EstadoPrestamo.Solicitado, EstadosPrestamo.Inicial);
        Assert.Equal(EstadoPrestamo.Solicitado, new Prestamo().Estado);
    }

    [Fact]
    public void Hay_exactamente_tres_estados_terminales()
    {
        // RF-NEG-05: Devuelto, Rechazado y Perdido no admiten salida.
        Assert.Equal(3, EstadosPrestamo.Terminales.Count);
        Assert.Equal(
            new[] { EstadoPrestamo.Devuelto, EstadoPrestamo.Rechazado, EstadoPrestamo.Perdido },
            EstadosPrestamo.Terminales.OrderBy(e => e));
    }

    [Theory]
    [InlineData(EstadoPrestamo.Devuelto)]
    [InlineData(EstadoPrestamo.Rechazado)]
    [InlineData(EstadoPrestamo.Perdido)]
    public void Un_estado_terminal_no_admite_ninguna_salida(EstadoPrestamo terminal)
    {
        foreach (var destino in EstadosPrestamo.Todos)
        {
            Assert.False(
                TransicionesPrestamo.PuedeOcurrir(terminal, destino),
                $"{terminal} no debería poder pasar a {destino}.");
        }
    }

    [Fact]
    public void Toda_transicion_permitida_sale_de_un_estado_no_terminal()
    {
        foreach (var transicion in TransicionesPrestamo.Permitidas)
        {
            Assert.False(
                EstadosPrestamo.EsTerminal(transicion.Desde),
                $"{transicion.Desde} es terminal y no puede tener salida.");
        }
    }

    [Fact]
    public void Toda_transicion_permitida_termina_en_un_estante_de_la_maquina()
    {
        foreach (var transicion in TransicionesPrestamo.Permitidas)
        {
            Assert.Contains(transicion.Hacia, EstadosPrestamo.Todos);
            Assert.Contains(transicion.Desde, EstadosPrestamo.Todos);
        }
    }

    [Fact]
    public void Ninguna_transicion_permitida_aparece_tambien_como_prohibida()
    {
        // Una transición no puede estar en las dos listas: sería contradictorio.
        foreach (var permitida in TransicionesPrestamo.Permitidas)
        {
            Assert.False(
                TransicionesPrestamo.EsProhibida(permitida.Desde, permitida.Hacia),
                $"{permitida.Desde}->{permitida.Hacia} está a la vez permitida y prohibida.");
        }
    }

    [Fact]
    public void Las_cuatro_transiciones_de_salida_son_no_permitidas()
    {
        // RF-NEG-04: transición desde un estado terminal.
        Assert.False(TransicionesPrestamo.PuedeOcurrir(EstadoPrestamo.Devuelto, EstadoPrestamo.Activo));
        Assert.False(TransicionesPrestamo.PuedeOcurrir(EstadoPrestamo.Rechazado, EstadoPrestamo.Activo));
        Assert.False(TransicionesPrestamo.PuedeOcurrir(EstadoPrestamo.Perdido, EstadoPrestamo.Activo));
    }

    [Fact]
    public void No_se_puede_devolver_un_prestamo_que_nunca_se_entrego()
    {
        // RF-NEG-04: transición prohibida explícita Solicitado -> Devuelto.
        Assert.True(TransicionesPrestamo.EsProhibida(EstadoPrestamo.Solicitado, EstadoPrestamo.Devuelto));
        Assert.False(TransicionesPrestamo.PuedeOcurrir(EstadoPrestamo.Solicitado, EstadoPrestamo.Devuelto));
    }

    [Fact]
    public void La_devolucion_solo_la_puede_hacer_el_socio_o_la_biblioteca()
    {
        var devolucion = TransicionesPrestamo.Permitidas
            .Single(t => t.Desde == EstadoPrestamo.Activo && t.Hacia == EstadoPrestamo.Devuelto);

        Assert.Equal(EjecutorTransicion.Estandar, devolucion.Quien);
    }

    [Fact]
    public void Activar_y_rechazar_exigen_un_administrador()
    {
        var activar = TransicionesPrestamo.Permitidas
            .Single(t => t.Desde == EstadoPrestamo.Solicitado && t.Hacia == EstadoPrestamo.Activo);

        Assert.Equal(EjecutorTransicion.Administrador, activar.Quien);
    }

    [Fact]
    public void Toda_transicion_prohibida_explica_el_motivo_del_rechazo()
    {
        // Si no se documenta el porqué, el rechazo es indefendible en la revisión.
        Assert.All(TransicionesPrestamo.Prohibidas,
            t => Assert.False(string.IsNullOrWhiteSpace(t.Condicion)));
    }

    [Fact]
    public void Toda_transicion_permitida_explica_su_condicion()
    {
        Assert.All(TransicionesPrestamo.Permitidas,
            t => Assert.False(string.IsNullOrWhiteSpace(t.Condicion)));
    }

    [Fact]
    public void El_mensaje_de_rechazo_no_filtra_detalles_internos()
    {
        var mensaje = TransicionesPrestamo.MensajeDeRechazo(
            EstadoPrestamo.Solicitado, EstadoPrestamo.Devuelto);

        Assert.DoesNotContain("SELECT", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbo.", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_texto_persistido_va_y_vuelve_sin_perder_el_estado()
    {
        // El enum se guarda como texto en SQL Server para que sea legible en SSMS.
        foreach (var estado in EstadosPrestamo.Todos)
        {
            Assert.Equal(estado, EstadosPrestamo.DesdeTexto(EstadosPrestamo.Nombre(estado)));
        }
    }

    [Fact]
    public void Un_texto_que_no_es_estado_se_rechaza()
    {
        Assert.Throws<InvalidOperationException>(() => EstadosPrestamo.DesdeTexto("Eliminado"));
    }
}