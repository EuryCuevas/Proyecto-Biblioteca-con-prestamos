using System;

namespace BibliotecaCore.Entities
{
    public class Lector
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string EstadoMembresia { get; set; } = "Activa"; // Activa, Suspendida
    }
}
