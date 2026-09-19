using PedidosService.Services;

namespace PedidosService.Tests;

// Reemplaza a NominatimGeocodingService en los tests: devuelve coordenadas
// fijas y predecibles según la dirección recibida, sin pegarle a internet.
public class FakeGeocodingService : IGeocodingService
{
    public Task<Coordenadas?> GeocodificarAsync(string direccion)
    {
        // Direcciones "conocidas" con coordenadas fijas, para poder
        // calcular a mano la distancia esperada en los tests.
        return direccion switch
        {
            "Origen Test" => Task.FromResult<Coordenadas?>(new Coordenadas(-34.6037, -58.3816)), // Obelisco
            "Destino Test" => Task.FromResult<Coordenadas?>(new Coordenadas(-34.5631, -58.4560)), // Cabildo/Juramento aprox.
            "Direccion Inexistente" => Task.FromResult<Coordenadas?>(null),
            _ => Task.FromResult<Coordenadas?>(new Coordenadas(-34.6037, -58.3816))
        };
    }
}