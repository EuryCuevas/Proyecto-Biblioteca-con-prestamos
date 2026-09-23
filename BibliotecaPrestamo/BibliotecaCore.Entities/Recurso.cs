using System;

namespace BibliotecaCore.Entities
{
    public class Recurso
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Titulo { get; set; } = string.Empty;
        public string Autor { get; set; } = string.Empty;
        public string Isbn { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string EstadoRecurso { get; set; } = "Disponible"; // Disponible, Prestado, Mantenimiento
    }
}
