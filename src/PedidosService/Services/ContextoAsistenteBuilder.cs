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

        // Total real en la base, no solo los últimos 100 que se listan en detalle.
        var totalGeneral = await _db.Pedidos.CountAsync();

        // Conteo agregado por estado (sobre TODA la tabla, no solo los 100 mostrados),
        // así preguntas tipo "cuántos pedidos tengo" o "cuántos están pendientes"
        // no dependen de que el modelo cuente líneas.
        var porEstadoTotal = await _db.Pedidos
            .Include(p => p.Estado)
            .AsNoTracking()
            .GroupBy(p => p.Estado != null ? p.Estado.Nombre : "Sin estado")
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync();

        var mapaRepartidores = await ObtenerMapaRepartidoresAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"TOTAL DE PEDIDOS EN EL SISTEMA: {totalGeneral}");
        sb.AppendLine($"CONTEO POR ESTADO (total, sobre todos los pedidos): " +
                      string.Join(", ", porEstadoTotal.Select(x => $"{x.Estado}: {x.Cantidad}")));
        sb.AppendLine($"A continuación se detallan los últimos {pedidos.Count} pedidos (no todos, por límite de contexto):");
        sb.AppendLine();
        sb.AppendLine("DETALLE DE PEDIDOS RECIENTES:");

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
            var repartidores = await client.GetFromJsonAsync<List<UsuarioDto>>("api/usuarios/repartidores");
            return repartidores?.ToDictionary(r => r.Id, r => r.Nombre) ?? new();
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }

    private record UsuarioDto(int Id, string Nombre);
}