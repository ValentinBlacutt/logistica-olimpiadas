namespace PedidosService.Services;

public static class CalculadoraEnvio
{
    private const decimal CostoBase = 500m;
    private const decimal TarifaPorKm = 50m;

    public static double DistanciaEnKm(Coordenadas origen, Coordenadas destino)
    {
        const double radioTierraKm = 6371;

        var dLat = GradosARadianes(destino.Latitud - origen.Latitud);
        var dLon = GradosARadianes(destino.Longitud - origen.Longitud);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(GradosARadianes(origen.Latitud)) * Math.Cos(GradosARadianes(destino.Latitud)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return radioTierraKm * c;
    }

    public static decimal CalcularCosto(double distanciaKm)
    {
        return CostoBase + (TarifaPorKm * (decimal)distanciaKm);
    }

    private static double GradosARadianes(double grados) => grados * Math.PI / 180;
}