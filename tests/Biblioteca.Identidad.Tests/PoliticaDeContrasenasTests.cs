using Biblioteca.Identidad.Password;

namespace Biblioteca.Identidad.Tests;

/// <summary>
/// RF-CA-14: la contraseña debe tener al menos 8 caracteres, con letras y números.
/// </summary>
public class PoliticaDeContrasenasTests
{
    [Theory]
    [InlineData("corta1")]
    [InlineData("12345678")]
    [InlineData("abcdefgh")]
    public void Una_contrasena_que_no_cumple_se_rechaza_con_motivo(string contrasena)
    {
        var motivo = PoliticaDeContrasenas.Validar(contrasena);

        Assert.NotNull(motivo);
        Assert.False(string.IsNullOrWhiteSpace(motivo));
    }

    [Theory]
    [InlineData("Biblioteca1")]
    [InlineData("abc12345")]
    [InlineData("PassWord9")]
    public void Una_contrasena_valida_se_acepta(string contrasena)
    {
        Assert.Null(PoliticaDeContrasenas.Validar(contrasena));
        Assert.True(PoliticaDeContrasenas.Cumple(contrasena));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void Una_contrasena_ausente_se_rechaza(string? contrasena)
    {
        Assert.NotNull(PoliticaDeContrasenas.Validar(contrasena));
    }

    [Fact]
    public void El_limite_es_de_ocho_caracteres_no_mas_ni_menos()
    {
        Assert.False(PoliticaDeContrasenas.Cumple("abc1234"));   // 7
        Assert.True(PoliticaDeContrasenas.Cumple("abc12345"));   // 8
    }

    [Fact]
    public void El_motivo_dice_que_corregir_y_no_revela_del_almacenamiento()
    {
        var motivo = PoliticaDeContrasenas.Validar("corta1");

        Assert.NotNull(motivo);
        // RD-08: el mensaje va al usuario, no puede hablar del hash ni de la base.
        Assert.DoesNotContain("hash", motivo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbo.", motivo, StringComparison.OrdinalIgnoreCase);
    }
}