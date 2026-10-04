using Biblioteca.Identidad;

namespace Biblioteca.Identidad.Autorizacion;

/// <summary>
/// Declara qué operación del sistema expone la acción del controlador.
/// El controlador NO dice qué roles se requieren: sólo nombra la operación, y
/// <see cref="PoliticaDeOperaciones"/> decide. Así la exigencia de rol vive en un
/// solo punto (RF-CA-05) y cambiar un permiso no obliga a tocar el controlador.
/// </summary>
/// <param name="operacion">Clave declarada en <see cref="Operaciones"/>.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiereOperacionAttribute(string operacion) : Attribute
{
    public string Operacion { get; } = operacion;
}