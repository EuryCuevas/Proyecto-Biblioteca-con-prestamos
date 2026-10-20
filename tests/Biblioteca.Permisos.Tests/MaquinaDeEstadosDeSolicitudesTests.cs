using Biblioteca.Nucleo.Errores;
using Biblioteca.Permisos.Solicitudes;

namespace Biblioteca.Permisos.Tests;

/// <summary>
/// Suite unitaria de Gestión de permisos (pieza 2). Se ejercita directamente el dominio,
/// sin levantar la aplicación completa (RD-12).
/// 
/// Usa preparar / actuar / verificar. Cada prueba verifica un solo comportamiento.
/// </summary>
public class MaquinaDeEstadosDeSolicitudesTests
{
    [Fact]
    public void RF_GP_04_Rechazar_deja_la_solicitud_en_Rechazada_y_guarda_el_motivo()
    {
        // preparar
        var solicitante = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitante,
            Operacion = "usuarios.listar",
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = DateTimeOffset.UtcNow
        };
        var momento = DateTimeOffset.UtcNow;

        // actuar
        solicitud.Rechazar(admin, "No procede", momento);

        // verificar
        Assert.Equal(EstadoSolicitud.Rechazada, solicitud.Estado);
        Assert.Equal(admin, solicitud.RechazadaPorId);
        Assert.NotNull(solicitud.RechazadaEn);
        Assert.Equal("No procede", solicitud.MotivoRechazo);
        Assert.True(EstadosSolicitud.Terminales.Contains(solicitud.Estado));
    }

    [Fact]
    public void RF_GP_03_Aprobar_luego_Aplicar_deja_la_solicitud_en_Aplicada_y_guarda_quien_y_cuando()
    {
        // preparar
        var solicitante = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitante,
            Operacion = "usuarios.listar",
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = DateTimeOffset.UtcNow
        };
        var momento = DateTimeOffset.UtcNow;

        // actuar
        solicitud.Aprobar(admin, momento);
        solicitud.Aplicar(momento);

        // verificar
        Assert.Equal(EstadoSolicitud.Aplicada, solicitud.Estado);
        Assert.Equal(admin, solicitud.AprobadaPorId);
        Assert.NotNull(solicitud.AprobadaEn);
        Assert.NotNull(solicitud.AplicadaEn);
        Assert.True(EstadosSolicitud.Terminales.Contains(solicitud.Estado));
    }

    [Fact]
    public void RF_GP_06_Aprobar_dos_veces_se_rechaza_y_el_estado_no_cambia()
    {
        // preparar
        var solicitante = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitante,
            Operacion = "usuarios.listar",
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = DateTimeOffset.UtcNow
        };
        var momento = DateTimeOffset.UtcNow;
        solicitud.Aprobar(admin, momento);
        solicitud.Aplicar(momento);
        var estadoAntes = solicitud.Estado;

        // actuar
        var ex = Assert.Throws<ExcepcionDominio>(() => solicitud.Aprobar(admin, momento));

        // verificar
        Assert.Equal(estadoAntes, solicitud.Estado);
        Assert.Equal(TipoError.ReglaDeNegocio, ex.Tipo);
    }

    [Fact]
    public void RF_GP_06_Pasar_de_Rechazada_a_Aplicada_se_rechaza_y_el_estado_no_cambia()
    {
        // preparar
        var solicitante = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitante,
            Operacion = "usuarios.listar",
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = DateTimeOffset.UtcNow
        };
        var momento = DateTimeOffset.UtcNow;
        solicitud.Rechazar(admin, "Denegada", momento);
        var estadoAntes = solicitud.Estado;

        // actuar
        var ex = Assert.Throws<ExcepcionDominio>(() => solicitud.Aplicar(momento));

        // verificar
        Assert.Equal(estadoAntes, solicitud.Estado);
        Assert.Equal(TipoError.ReglaDeNegocio, ex.Tipo);
    }

    [Fact]
    public void RF_GP_04_Rechazar_sin_motivo_no_completa_el_rechazo()
    {
        // preparar
        var solicitante = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitante,
            Operacion = "usuarios.listar",
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = DateTimeOffset.UtcNow
        };
        var momento = DateTimeOffset.UtcNow;
        var estadoAntes = solicitud.Estado;

        // actuar
        var ex = Assert.Throws<ExcepcionDominio>(() => solicitud.Rechazar(admin, "   ", momento));

        // verificar
        Assert.Equal(estadoAntes, solicitud.Estado);
        Assert.Equal(TipoError.Validacion, ex.Tipo);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Fact]
    public void RF_GP_07_Un_administrador_no_puede_resolver_su_propia_solicitud()
    {
        // preparar
        var admin = Guid.NewGuid();
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = admin,
            Operacion = "usuarios.listar",
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = DateTimeOffset.UtcNow
        };
        var momento = DateTimeOffset.UtcNow;
        var estadoAntes = solicitud.Estado;

        // actuar
        var ex = Assert.Throws<ExcepcionDominio>(() => solicitud.Aprobar(admin, momento));

        // verificar
        Assert.Equal(estadoAntes, solicitud.Estado);
        Assert.Equal(TipoError.ReglaDeNegocio, ex.Tipo);
    }

    [Fact]
    public void RF_GP_06_Las_transiciones_solo_son_las_declaradas_en_la_tabla_unica()
    {
        // preparar
        var solicitante = Guid.NewGuid();
        var admin = Guid.NewGuid();

        // actuar / verificar - transiciones válidas
        var s1 = new SolicitudPermiso { SolicitanteId = solicitante, Operacion = "x", Estado = EstadoSolicitud.Pendiente, CreadaEn = DateTimeOffset.UtcNow };
        s1.Aprobar(admin, DateTimeOffset.UtcNow);
        s1.Aplicar(DateTimeOffset.UtcNow);
        Assert.Equal(EstadoSolicitud.Aplicada, s1.Estado);

        var s2 = new SolicitudPermiso { SolicitanteId = solicitante, Operacion = "x", Estado = EstadoSolicitud.Pendiente, CreadaEn = DateTimeOffset.UtcNow };
        s2.Rechazar(admin, "no", DateTimeOffset.UtcNow);
        Assert.Equal(EstadoSolicitud.Rechazada, s2.Estado);

        // verificar - prohibidas fuera de tabla
        Assert.False(TransicionesSolicitud.EsPermitida(EstadoSolicitud.Rechazada, EstadoSolicitud.Pendiente));
        Assert.False(TransicionesSolicitud.EsPermitida(EstadoSolicitud.Aplicada, EstadoSolicitud.Pendiente));
        Assert.False(TransicionesSolicitud.EsPermitida(EstadoSolicitud.Aprobada, EstadoSolicitud.Rechazada));
        Assert.False(TransicionesSolicitud.EsPermitida(EstadoSolicitud.Pendiente, EstadoSolicitud.Aplicada));
    }
}
