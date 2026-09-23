using System;

namespace BibliotecaCore.Entities
{
    public class Prestamo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RecursoId { get; set; } = string.Empty;
        public string LectorId { get; set; } = string.Empty;
        public DateTime FechaPrestamo { get; set; } = DateTime.UtcNow;
        public DateTime FechaVencimiento { get; set; } = DateTime.UtcNow.AddDays(14);
        public DateTime? FechaDevolucion { get; set; }
        public string Estado { get; set; } = "Solicitado"; // Solicitado, Activo, Devuelto, Vencido
    }
}
