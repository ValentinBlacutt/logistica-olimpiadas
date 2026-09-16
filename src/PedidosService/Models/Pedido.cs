namespace PedidosService.Models;

public class Pedido
{
    public int Id { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }

    public int EstadoId { get; set; }
    public Estado Estado { get; set; } = null!;

    public decimal CostoEnvio { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;

    // Referencia lógica al Usuario.Id de AuthService (rol Repartidor).
    // Sin FK física: el desacoplamiento entre bases se resuelve a nivel aplicación,
    // no a nivel de base de datos.
    public int? RepartidorId { get; set; }
}