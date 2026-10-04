using Biblioteca.Identidad.Password;

namespace Biblioteca.Identidad.Tests;

/// <summary>
/// Verifica que los tokens de un solo uso se generan con suficiente entropía y que
/// nunca se persiste el valor en claro (RF-CA-15, RF-CA-10, RF-AUD-05).
/// </summary>
public class GeneradorDeSecretosTests
{
    [Fact]
    public void Dos_tokens_nunca_son_iguales()
    {
        var tokens = Enumerable.Range(0, 500)
            .Select(_ => GeneradorDeSecretos.NuevoToken())
            .ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void El_token_no_lleva_caracteres_que_rompan_una_url()
    {
        // El token viaja dentro de un enlace de correo. Si llevara '+', '/' o '=', el
        // enlace se rompería o se codificaría de forma distinta en cada cliente.
        for (var i = 0; i < 100; i++)
        {
            var token = GeneradorDeSecretos.NuevoToken();

            Assert.DoesNotContain('+', token);
            Assert.DoesNotContain('/', token);
            Assert.DoesNotContain('=', token);
            Assert.All(token, c => Assert.True(
                char.IsLetterOrDigit(c) || c == '-' || c == '_',
                $"Carácter inesperado en el token: '{c}'."));
        }
    }

    [Fact]
    public void El_token_tiene_entropia_suficiente()
    {
        // 256 bits en base64url ≈ 43 caracteres.
        Assert.InRange(GeneradorDeSecretos.NuevoToken().Length, 40, 48);
    }

    [Fact]
    public void Lo_que_se_persiste_es_el_hash_no_el_token()
    {
        var token = GeneradorDeSecretos.NuevoToken();
        var hash = GeneradorDeSecretos.Hash(token);

        Assert.Equal(32, hash.Length); // SHA-256
        Assert.DoesNotContain(token, Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_mismo_token_produce_siempre_el_mismo_hash()
    {
        var token = GeneradorDeSecretos.NuevoToken();

        Assert.Equal(GeneradorDeSecretos.Hash(token), GeneradorDeSecretos.Hash(token));
    }

    [Fact]
    public void La_comparacion_rechaza_un_hash_distinto()
    {
        var token = GeneradorDeSecretos.NuevoToken();
        var otro = GeneradorDeSecretos.NuevoToken();

        Assert.False(GeneradorDeSecretos.Coincide(
            GeneradorDeSecretos.Hash(token),
            GeneradorDeSecretos.Hash(otro)));
    }

    [Fact]
    public void La_comparacion_acepta_el_hash_correcto()
    {
        var token = GeneradorDeSecretos.NuevoToken();

        Assert.True(GeneradorDeSecretos.Coincide(
            GeneradorDeSecretos.Hash(token),
            GeneradorDeSecretos.Hash(token)));
    }
}