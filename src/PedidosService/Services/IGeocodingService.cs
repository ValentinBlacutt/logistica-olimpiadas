namespace PedidosService.Services;

public record Coordenadas(double Latitud, double Longitud);

public interface IGeocodingService
{
    Task<Coordenadas?> GeocodificarAsync(string direccion);
}