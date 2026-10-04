using Biblioteca.Biblioteca.Prestamos;

namespace Biblioteca.Biblioteca.Catalogo;

/// <summary>
/// Copia física de un <see cref="Recurso"/>. Es la unidad que se presta y se devuelve.
/// </summary>
/// <remarks>
/// <para>
/// Esta entidad <b>no tiene columna «Disponible»</b>. La disponibilidad se deriva de
/// los préstamos que retienen la copia, y la base impide que haya dos a la vez con el
/// índice único filtrado <c>UX_Prestamo_EjemplarVivo</c>.
/// </para>
/// <para>
/// La razón: una bandera booleana mantenida a mano son dos verdades —la columna y los
/// préstamos— y se desincroniza en cuanto una actualización falla a mitad. Derivándola
/// queda una sola, y el índice convierte la regla en algo que el motor garantiza.
/// </para>
/// </remarks>
public sealed class Ejemplar
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecursoId { get; set; }

    public Recurso? Recurso { get; set; }

    /// <summary>Etiqueta interna de la copia. Distinta por ejemplar.</summary>
    public string Codigo { get; set; } = string.Empty;

    public DateTimeOffset IngresadoEn { get; set; }

    /// <summary>
    /// Copia dada de baja por pérdida o deterioro. Deja de ser prestable, pero sus
    /// préstamos antiguos se conservan.
    /// </summary>
    public bool Retirado { get; set; }

    public ICollection<Prestamo> Prestamos { get; set; } = [];
}