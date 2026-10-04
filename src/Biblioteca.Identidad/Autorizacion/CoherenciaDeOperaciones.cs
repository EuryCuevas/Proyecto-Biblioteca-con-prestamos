namespace Biblioteca.Identidad.Autorizacion;

/// <summary>
/// Reglas de coherencia entre las acciones HTTP y la política de operaciones.
/// La lógica es estática y sin dependencias de MVC para poder probarla directamente
/// (RD-12).
/// </summary>
public static class CoherenciaDeOperaciones
{
    /// <summary>
    /// Valida una acción. Lanza <see cref="InvalidOperationException"/> si la acción
    /// no declara operación o si la operación no existe en la política.
    /// </summary>
    /// <param name="accion">Descripción de la acción, para el mensaje del fallo.</param>
    /// <param name="operacion">
    /// Clave declarada, o <see langword="null"/> si la acción no trae
    /// <see cref="RequiereOperacionAttribute"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// La acción no cumple RF-CA-05. Se lanza al construir el modelo de MVC, de modo
    /// que la aplicación no llega a escuchar.
    /// </exception>
    public static void Validar(string accion, string? operacion)
    {
        if (string.IsNullOrWhiteSpace(operacion))
        {
            throw new InvalidOperationException(
                $"RF-CA-05: la acción '{accion}' no declara operación. " +
                "Decorarla con [RequiereOperacion(Operaciones.X)].");
        }

        if (!PoliticaDeOperaciones.EstaDeclarada(operacion))
        {
            throw new InvalidOperationException(
                $"RF-CA-05: la acción '{accion}' declara la operación '{operacion}', " +
                "que no existe en PoliticaDeOperaciones.Requisitos.");
        }
    }
}