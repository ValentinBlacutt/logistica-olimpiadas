using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedidosService.Data;
using PedidosService.Models;

namespace PedidosService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly PedidosDbContext _db;

    public PedidosController(PedidosDbContext db)
    {
        _db = db;
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
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Crear(CrearPedidoRequest request)
    {
        var estadoPendiente = await _db.Estados.FirstAsync(e => e.Nombre == "Pendiente");

        var pedido = new Pedido
        {
            Direccion = request.Direccion,
            Latitud = request.Latitud,
            Longitud = request.Longitud,
            ClienteNombre = request.ClienteNombre,
            ClienteTelefono = request.ClienteTelefono,
            EstadoId = estadoPendiente.Id,
            CostoEnvio = 0 // se calculará más adelante con la integración externa de geocoding
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
}