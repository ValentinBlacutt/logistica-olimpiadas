namespace PedidosService.Models;

public class PedidoResponseDto
{
    public int Id { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public decimal CostoEnvio { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public int? RepartidorId { get; set; }

    public static PedidoResponseDto FromPedido(Pedido pedido) => new()
    {
        Id = pedido.Id,
        Direccion = pedido.Direccion,
        Latitud = pedido.Latitud,
        Longitud = pedido.Longitud,
        EstadoNombre = pedido.Estado?.Nombre ?? string.Empty,
        CostoEnvio = pedido.CostoEnvio,
        FechaCreacion = pedido.FechaCreacion,
        ClienteNombre = pedido.ClienteNombre,
        ClienteTelefono = pedido.ClienteTelefono,
        RepartidorId = pedido.RepartidorId
    };
}