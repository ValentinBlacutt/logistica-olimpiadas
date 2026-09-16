using System.Text.Json;

namespace PedidosService.Services;

public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(HttpClient httpClient, ILogger<NominatimGeocodingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LogisticaOlimpiadas/1.0");
        }
    }

    public async Task<Coordenadas?> GeocodificarAsync(string direccion)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(direccion)}&format=json&limit=1";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim devolvió {StatusCode} para la dirección '{Direccion}'", response.StatusCode, direccion);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.GetArrayLength() == 0)
            {
                _logger.LogWarning("Nominatim no encontró resultados para la dirección '{Direccion}'", direccion);
                return null;
            }

            var primerResultado = doc.RootElement[0];
            var lat = double.Parse(primerResultado.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var lon = double.Parse(primerResultado.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

            return new Coordenadas(lat, lon);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al geocodificar la dirección '{Direccion}'", direccion);
            return null;
        }
    }
}