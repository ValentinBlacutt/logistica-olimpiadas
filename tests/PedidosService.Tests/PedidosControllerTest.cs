using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedidosService.Data;
using PedidosService.Models;
using Xunit;

namespace PedidosService.Tests;

public class PedidosControllerTests : IClassFixture<PedidosApiFactory>
{
    private readonly PedidosApiFactory _factory;
    private readonly HttpClient _client;

    public PedidosControllerTests(PedidosApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        SeedEstados();
    }

    private void SeedEstados()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PedidosDbContext>();

        if (!db.Estados.Any())
        {
            db.Estados.AddRange(
                new Estado { Nombre = "Pendiente" },
                new Estado { Nombre = "Asignado" },
                new Estado { Nombre = "En camino" },
                new Estado { Nombre = "Entregado" },
                new Estado { Nombre = "Cancelado" }
            );
            db.SaveChanges();
        }
    }

    private void ConToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Crear_ComoAdministrador_DevuelveCreatedConCostoCalculado()
    {
        ConToken(TestTokenHelper.GenerarToken(1, "admin@test.com", "Administrador"));

        var request = new CrearPedidoRequest
        {
            DireccionOrigen = "Origen Test",
            DireccionDestino = "Destino Test",
            ClienteNombre = "Juan Perez",
            ClienteTelefono = "1155554444"
        };

        var response = await _client.PostAsJsonAsync("/api/pedidos", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var pedido = await response.Content.ReadFromJsonAsync<PedidoResponseDto>();
        Assert.NotNull(pedido);
        Assert.True(pedido!.CostoEnvio > 500); // costo base + algo de distancia
        Assert.Equal("Pendiente", pedido.EstadoNombre);
    }

    [Fact]
    public async Task Crear_ComoRepartidor_Devuelve403()
    {
        ConToken(TestTokenHelper.GenerarToken(2, "repartidor@test.com", "Repartidor", repartidorId: 2));

        var request = new CrearPedidoRequest
        {
            DireccionOrigen = "Origen Test",
            DireccionDestino = "Destino Test",
            ClienteNombre = "Juan Perez",
            ClienteTelefono = "1155554444"
        };

        var response = await _client.PostAsJsonAsync("/api/pedidos", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConDireccionInexistente_Devuelve400()
    {
        ConToken(TestTokenHelper.GenerarToken(1, "admin@test.com", "Administrador"));

        var request = new CrearPedidoRequest
        {
            DireccionOrigen = "Direccion Inexistente",
            DireccionDestino = "Destino Test",
            ClienteNombre = "Juan Perez",
            ClienteTelefono = "1155554444"
        };

        var response = await _client.PostAsJsonAsync("/api/pedidos", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Listar_SinToken_Devuelve401()
    {
        var response = await _client.GetAsync("/api/pedidos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AsignarYActualizarEstado_FlujoCompleto_Funciona()
    {
        // Crear el pedido como admin
        ConToken(TestTokenHelper.GenerarToken(1, "admin@test.com", "Administrador"));
        var crearRequest = new CrearPedidoRequest
        {
            DireccionOrigen = "Origen Test",
            DireccionDestino = "Destino Test",
            ClienteNombre = "Ana Gomez",
            ClienteTelefono = "1166667777"
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/pedidos", crearRequest);
        var pedidoCreado = await crearResponse.Content.ReadFromJsonAsync<PedidoResponseDto>();

        // Asignar repartidor (id 5) como admin
        var asignarResponse = await _client.PostAsJsonAsync(
            $"/api/pedidos/{pedidoCreado!.Id}/asignar",
            new AsignarRepartidorRequest { RepartidorId = 5 });

        Assert.Equal(HttpStatusCode.OK, asignarResponse.StatusCode);
        var pedidoAsignado = await asignarResponse.Content.ReadFromJsonAsync<PedidoResponseDto>();
        Assert.Equal("Asignado", pedidoAsignado!.EstadoNombre);

        // El repartidor dueño (id 5) actualiza el estado — debería poder
        ConToken(TestTokenHelper.GenerarToken(5, "repartidor5@test.com", "Repartidor", repartidorId: 5));
        var estadoResponse = await _client.PutAsJsonAsync(
            $"/api/pedidos/{pedidoCreado.Id}/estado",
            new ActualizarEstadoRequest { NombreEstado = "En camino" });

        Assert.Equal(HttpStatusCode.OK, estadoResponse.StatusCode);

        // Otro repartidor (id 999) intenta tocar el mismo pedido — no debería poder
        ConToken(TestTokenHelper.GenerarToken(999, "otro@test.com", "Repartidor", repartidorId: 999));
        var estadoAjenoResponse = await _client.PutAsJsonAsync(
            $"/api/pedidos/{pedidoCreado.Id}/estado",
            new ActualizarEstadoRequest { NombreEstado = "Entregado" });

        Assert.Equal(HttpStatusCode.Forbidden, estadoAjenoResponse.StatusCode);
    }

    [Fact]
    public async Task Cancelar_ComoAdministrador_CambiaEstadoACancelado()
    {
        ConToken(TestTokenHelper.GenerarToken(1, "admin@test.com", "Administrador"));

        var crearResponse = await _client.PostAsJsonAsync("/api/pedidos", new CrearPedidoRequest
        {
            DireccionOrigen = "Origen Test",
            DireccionDestino = "Destino Test",
            ClienteNombre = "Cliente Cancelado",
            ClienteTelefono = "1100001111"
        });
        var pedidoCreado = await crearResponse.Content.ReadFromJsonAsync<PedidoResponseDto>();

        var cancelarResponse = await _client.DeleteAsync($"/api/pedidos/{pedidoCreado!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancelarResponse.StatusCode);

        var detalleResponse = await _client.GetAsync($"/api/pedidos/{pedidoCreado.Id}");
        var detalle = await detalleResponse.Content.ReadFromJsonAsync<PedidoResponseDto>();
        Assert.Equal("Cancelado", detalle!.EstadoNombre);
    }
}