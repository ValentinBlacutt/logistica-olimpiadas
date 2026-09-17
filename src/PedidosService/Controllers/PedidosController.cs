using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedidosService.Data;
using PedidosService.Models;
using PedidosService.Services;

namespace PedidosService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly PedidosDbContext _db;
    private readonly IGeocodingService _geocoding;

    public PedidosController(PedidosDbContext db, IGeocodingService geocoding)
    {
        _db = db;
        _geocoding = geocoding;
    }

    // GET /api/pedidos — listado completo (Administrador)
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Listar()
    {
        var pedidos = await _db.Pedidos.Include(p => p.Estado).ToListAsync();
        return Ok(pedidos.Select(PedidoResponseDto.FromPedido));
    }

    // GET /api/pedidos/mis-pedidos — pedidos asignados al repartidor autenticado
    [HttpGet("mis-pedidos")]
    [Authorize(Roles = "Repartidor")]
    public async Task<IActionResult> MisPedidos()
    {
        var repartidorId = int.Parse(User.FindFirst("repartidor_id")!.Value);

        var pedidos = await _db.Pedidos
            .Include(p => p.Estado)
            .Where(p => p.RepartidorId == repartidorId)
            .ToListAsync();

        return Ok(pedidos.Select(PedidoResponseDto.FromPedido));
    }

    // GET /api/pedidos/{id} — detalle de un pedido
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Detalle(int id)
    {
        var pedido = await _db.Pedidos.Include(p => p.Estado).FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();
        return Ok(PedidoResponseDto.FromPedido(pedido));
    }

    // POST /api/pedidos — crear pedido (Administrador)
    // Geocodifica origen y destino con Nominatim, calcula distancia (Haversine)
    // y el costo de envío (costo base + tarifa por km).
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Crear(CrearPedidoRequest request)
    {
        var origen = await _geocoding.GeocodificarAsync(request.DireccionOrigen);
        if (origen is null)
            return BadRequest($"No se pudo geocodificar la dirección de origen: '{request.DireccionOrigen}'");

        var destino = await _geocoding.GeocodificarAsync(request.DireccionDestino);
        if (destino is null)
            return BadRequest($"No se pudo geocodificar la dirección de destino: '{request.DireccionDestino}'");

        var distanciaKm = CalculadoraEnvio.DistanciaEnKm(origen, destino);
        var costoEnvio = CalculadoraEnvio.CalcularCosto(distanciaKm);

        var estadoPendiente = await _db.Estados.FirstAsync(e => e.Nombre == "Pendiente");

        var pedido = new Pedido
        {
            OrigenDireccion = request.DireccionOrigen,
            OrigenLatitud = origen.Latitud,
            OrigenLongitud = origen.Longitud,
            Direccion = request.DireccionDestino,
            Latitud = destino.Latitud,
            Longitud = destino.Longitud,
            ClienteNombre = request.ClienteNombre,
            ClienteTelefono = request.ClienteTelefono,
            EstadoId = estadoPendiente.Id,
            CostoEnvio = costoEnvio
        };

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();

        pedido.Estado = estadoPendiente; // ya lo tenemos en memoria, evita un round-trip a la DB

        return CreatedAtAction(nameof(Detalle), new { id = pedido.Id }, PedidoResponseDto.FromPedido(pedido));
    }

    // POST /api/pedidos/{id}/asignar — asignar repartidor (Administrador)
    [HttpPost("{id}/asignar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsignarRepartidor(int id, AsignarRepartidorRequest request)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido is null) return NotFound();

        var estadoAsignado = await _db.Estados.FirstAsync(e => e.Nombre == "Asignado");

        pedido.RepartidorId = request.RepartidorId;
        pedido.EstadoId = estadoAsignado.Id;

        await _db.SaveChangesAsync();

        pedido.Estado = estadoAsignado;

        return Ok(PedidoResponseDto.FromPedido(pedido));
    }

    // PUT /api/pedidos/{id}/estado — actualizar estado (Administrador o el Repartidor asignado)
    [HttpPut("{id}/estado")]
    [Authorize(Roles = "Administrador,Repartidor")]
    public async Task<IActionResult> ActualizarEstado(int id, ActualizarEstadoRequest request)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido is null) return NotFound();

        // Si es Repartidor, solo puede tocar sus propios pedidos asignados.
        if (User.IsInRole("Repartidor"))
        {
            var repartidorId = int.Parse(User.FindFirst("repartidor_id")!.Value);
            if (pedido.RepartidorId != repartidorId)
                return Forbid();
        }

        var nuevoEstado = await _db.Estados.FirstOrDefaultAsync(e => e.Nombre == request.NombreEstado);
        if (nuevoEstado is null)
            return BadRequest($"El estado '{request.NombreEstado}' no existe.");

        pedido.EstadoId = nuevoEstado.Id;
        await _db.SaveChangesAsync();

        pedido.Estado = nuevoEstado;

        return Ok(PedidoResponseDto.FromPedido(pedido));
    }

    // DELETE /api/pedidos/{id} — cancelar pedido (Administrador). No borra físicamente, cambia el estado.
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido is null) return NotFound();

        var estadoCancelado = await _db.Estados.FirstAsync(e => e.Nombre == "Cancelado");
        pedido.EstadoId = estadoCancelado.Id;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/pedidos/mis-pedidos/ruta-optima?latActual=&lonActual=
    // Devuelve los pedidos pendientes del repartidor, reordenados por la ruta más corta.
    [HttpGet("mis-pedidos/ruta-optima")]
    [Authorize(Roles = "Repartidor")]
    public async Task<IActionResult> RutaOptima([FromQuery] double? latActual, [FromQuery] double? lonActual)
    {
        var repartidorId = int.Parse(User.FindFirst("repartidor_id")!.Value);

        var pedidos = await _db.Pedidos
            .Include(p => p.Estado)
            .Where(p => p.RepartidorId == repartidorId
                && p.Estado.Nombre != "Entregado"
                && p.Estado.Nombre != "Cancelado")
            .ToListAsync();

        if (pedidos.Count == 0)
            return Ok(Array.Empty<PedidoResponseDto>());

        // Si no mandan la posición actual del repartidor, arrancamos
        // desde el origen del primer pedido como aproximación.
        var puntoInicial = (latActual.HasValue && lonActual.HasValue)
            ? new Coordenadas(latActual.Value, lonActual.Value)
            : new Coordenadas(pedidos[0].OrigenLatitud, pedidos[0].OrigenLongitud);

        var ordenados = OptimizadorRuta.OrdenarPorRutaOptima(pedidos, puntoInicial);

        return Ok(ordenados.Select(PedidoResponseDto.FromPedido));
    }
}