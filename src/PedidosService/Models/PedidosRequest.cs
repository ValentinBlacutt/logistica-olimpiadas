namespace PedidosService.Models;

public class CrearPedidoRequest
{
    public string Direccion { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
}

public class AsignarRepartidorRequest
{
    public int RepartidorId { get; set; }
}

public class ActualizarEstadoRequest
{
    public string NombreEstado { get; set; } = string.Empty;
}