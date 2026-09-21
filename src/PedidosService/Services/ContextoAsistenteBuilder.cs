using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PedidosService.Data;

namespace PedidosService.Services;

public class ContextoAsistenteBuilder
{
    private readonly PedidosDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public ContextoAsistenteBuilder(PedidosDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ConstruirAsync()
    {
        var pedidos = await _db.Pedidos
            .Include(p => p.Estado)
            .AsNoTracking()
            .OrderByDescending(p => p.FechaCreacion)
            .Take(100)
            .ToListAsync();

        if (!pedidos.Any())
            return "No hay pedidos registrados en el sistema actualmente.";

        // Obtener nombres de repartidores desde AuthService (opcional/resiliente)
        var mapaRepartidores = await ObtenerMapaRepartidoresAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("RESUMEN DE PEDIDOS RECIENTES:");

        foreach (var p in pedidos)
        {
            string repartidorInfo = "Sin asignar";
            if (p.RepartidorId.HasValue)
            {
                repartidorInfo = mapaRepartidores.TryGetValue(p.RepartidorId.Value, out var nombre)
                    ? $"{nombre} (ID: {p.RepartidorId.Value})"
                    : $"Repartidor ID {p.RepartidorId.Value}";
            }

            sb.AppendLine($"- Pedido #{p.Id} | Cliente: {p.ClienteNombre} | Estado: {p.Estado?.Nombre ?? "Sin estado"} | Repartidor: {repartidorInfo} | Destino: {p.Direccion} | Costo Envío: ${p.CostoEnvio:N2} | Fecha: {p.FechaCreacion:yyyy-MM-dd HH:mm}");
        }

        return sb.ToString();
    }

    private async Task<Dictionary<int, string>> ObtenerMapaRepartidoresAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AuthService");
            // Endpoint ficticio en tu AuthService que devuelva lista de usuarios/repartidores
            var repartidores = await client.GetFromJsonAsync<List<UsuarioDto>>("api/usuarios/repartidores");
            return repartidores?.ToDictionary(r => r.Id, r => r.Nombre) ?? new();
        }
        catch
        {
            // Si falla AuthService, el sistema sigue funcionando usando solo IDs
            return new Dictionary<int, string>();
        }
    }

    private record UsuarioDto(int Id, string Nombre);
}