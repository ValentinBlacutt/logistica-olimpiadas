using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PedidosService.Data;
using PedidosService.Models;
using PedidosService.Services;

namespace PedidosService.Controllers;

public record PreguntaIARequest(string Pregunta);

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly PedidosDbContext _db;
    private readonly IGeocodingService _geocoding;
    private readonly IAsistenteService _asistente;
    private readonly ContextoAsistenteBuilder _contextoBuilder;
    private readonly IMemoryCache _cache;

    public PedidosController(
        PedidosDbContext db,
        IGeocodingService geocoding,
        IAsistenteService asistente,
        ContextoAsistenteBuilder contextoBuilder,
        IMemoryCache cache)
    {
        _db = db;
        _geocoding = geocoding;
        _asistente = asistente;
        _contextoBuilder = contextoBuilder;
        _cache = cache;
    }

    // GET /api/pedidos
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Listar()
    {
        var pedidos = await _db.Pedidos.Include(p => p.Estado).ToListAsync();
        return Ok(pedidos.Select(PedidoResponseDto.FromPedido));
    }

    // GET /api/pedidos/mis-pedidos
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

    // GET /api/pedidos/{id}
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Detalle(int id)
    {
        var pedido = await _db.Pedidos.Include(p => p.Estado).FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();
        return Ok(PedidoResponseDto.FromPedido(pedido));
    }

    // POST /api/pedidos
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

        pedido.Estado = estadoPendiente;

        return CreatedAtAction(nameof(Detalle), new { id = pedido.Id }, PedidoResponseDto.FromPedido(pedido));
    }

    // POST /api/pedidos/{id}/asignar
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

    // PUT /api/pedidos/{id}/estado
    [HttpPut("{id}/estado")]
    [Authorize(Roles = "Administrador,Repartidor")]
    public async Task<IActionResult> ActualizarEstado(int id, ActualizarEstadoRequest request)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido is null) return NotFound();

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

    // DELETE /api/pedidos/{id}
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

    // GET /api/pedidos/mis-pedidos/ruta-optima
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

        var puntoInicial = (latActual.HasValue && lonActual.HasValue)
            ? new Coordenadas(latActual.Value, lonActual.Value)
            : new Coordenadas(pedidos[0].OrigenLatitud, pedidos[0].OrigenLongitud);

        var ordenados = OptimizadorRuta.OrdenarPorRutaOptima(pedidos, puntoInicial);

        return Ok(ordenados.Select(PedidoResponseDto.FromPedido));
    }

    // POST /api/pedidos/asistente-ia — Consulta tradicional en bloque
    [HttpPost("asistente-ia")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsistenteIA([FromBody] PreguntaIARequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
            return BadRequest(new { error = "La pregunta no puede estar vacía." });

        try
        {
            var contexto = await ObtenerContextoConCacheAsync();
            var respuesta = await _asistente.ConsultarAsync(request.Pregunta, contexto);
            return Ok(new { respuesta });
        }
        catch (TimeoutException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = "El servicio de IA tardó demasiado en responder. Intente nuevamente."
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "El servicio de IA no está disponible temporalmente por alta demanda."
            });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Ocurrió un error inesperado al procesar la solicitud con el asistente."
            });
        }
    }

    // POST /api/pedidos/asistente-ia/stream — Streaming por Server-Sent Events (SSE)
    [HttpPost("asistente-ia/stream")]
    [Authorize(Roles = "Administrador")]
    public async Task ConsultarStream([FromBody] PreguntaIARequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("La pregunta no puede estar vacía.", cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            var contexto = await ObtenerContextoConCacheAsync();

            await foreach (var chunk in _asistente.ConsultarStreamAsync(request.Pregunta, contexto, cancellationToken))
            {
                await Response.WriteAsync($"data: {chunk.Replace("\n", "\\n")}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // El usuario canceló la request HTTP / cerró la solapa del navegador
        }
        catch (Exception)
        {
            await Response.WriteAsync("data: [ERROR_IA_TEMPORAL]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    private Task<string> ObtenerContextoConCacheAsync()
    {
        return _cache.GetOrCreateAsync("contexto-asistente-ia", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await _contextoBuilder.ConstruirAsync();
        })!;
    }
}