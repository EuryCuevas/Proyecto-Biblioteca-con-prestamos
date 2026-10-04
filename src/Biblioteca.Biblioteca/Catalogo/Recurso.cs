namespace Biblioteca.Biblioteca.Catalogo;

/// <summary>
/// Obra del catálogo: el título que la biblioteca presta. No es una copia física;
/// de un recurso hay N <see cref="Ejemplar"/>.
/// </summary>
public sealed class Recurso
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Titulo { get; set; } = string.Empty;

    public string Autor { get; set; } = string.Empty;

    /// <summary>Identifica la obra. Único cuando existe: no todos los recursos lo tienen.</summary>
    public string? Isbn { get; set; }

    public string? Editorial { get; set; }

    public short? AnioPublicacion { get; set; }

    public string? Genero { get; set; }

    /// <summary>Un recurso retirado sale del catálogo nuevo, pero no del histórico.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Copias físicas de esta obra.</summary>
    public ICollection<Ejemplar> Ejemplares { get; set; } = [];
}