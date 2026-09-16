using Microsoft.EntityFrameworkCore;
using PedidosService.Models;

namespace PedidosService.Data;

public class PedidosDbContext : DbContext
{
    public PedidosDbContext(DbContextOptions<PedidosDbContext> options) : base(options) { }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Estado> Estados => Set<Estado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>()
            .Property(p => p.CostoEnvio)
            .HasColumnType("decimal(10,2)");

        modelBuilder.Entity<Estado>().HasData(
            new Estado { Id = 1, Nombre = "Pendiente" },
            new Estado { Id = 2, Nombre = "Asignado" },
            new Estado { Id = 3, Nombre = "En camino" },
            new Estado { Id = 4, Nombre = "Entregado" },
            new Estado { Id = 5, Nombre = "Cancelado" }
        );
    }
}