using System;

namespace BibliotecaCore.Entities
{
    public class Multa
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PrestamoId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string EstadoPago { get; set; } = "Pendiente"; // Pendiente, Pagada, Anulada
    }
}
