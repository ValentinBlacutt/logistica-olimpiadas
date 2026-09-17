using PedidosService.Models;

namespace PedidosService.Services;

public static class OptimizadorRuta
{
    // Heurística Nearest Neighbor: desde el punto actual, siempre salta
    // al pedido no visitado más cercano. No garantiza el óptimo absoluto,
    // pero para pocas paradas (reparto diario típico) da muy buenos resultados
    // con costo computacional mínimo.
    public static List<Pedido> OrdenarPorRutaOptima(List<Pedido> pedidos, Coordenadas puntoInicial)
    {
        var restantes = new List<Pedido>(pedidos);
        var ordenados = new List<Pedido>();
        var posicionActual = puntoInicial;

        while (restantes.Count > 0)
        {
            var masCercano = restantes
                .OrderBy(p => CalculadoraEnvio.DistanciaEnKm(
                    posicionActual, new Coordenadas(p.Latitud, p.Longitud)))
                .First();

            ordenados.Add(masCercano);
            restantes.Remove(masCercano);
            posicionActual = new Coordenadas(masCercano.Latitud, masCercano.Longitud);
        }

        return ordenados;
    }
}