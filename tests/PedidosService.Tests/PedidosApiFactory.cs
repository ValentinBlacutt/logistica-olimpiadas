using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedidosService.Data;
using PedidosService.Services;

namespace PedidosService.Tests;

public class PedidosApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "PedidosTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Saca todo lo relacionado a EF Core (SqlServer) que registró
            // el proyecto real, y lo reemplaza por InMemory.
            var descriptoresEf = services
                .Where(d => d.ServiceType.Namespace != null
                    && d.ServiceType.Namespace.StartsWith("Microsoft.EntityFrameworkCore"))
                .ToList();
            foreach (var descriptor in descriptoresEf)
                services.Remove(descriptor);

            services.AddDbContext<PedidosDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Reemplaza el geocoding real (Nominatim) por uno falso y predecible.
            var descriptorGeocoding = services
                .SingleOrDefault(d => d.ServiceType == typeof(IGeocodingService));
            if (descriptorGeocoding is not null)
                services.Remove(descriptorGeocoding);

            services.AddScoped<IGeocodingService, FakeGeocodingService>();
        });
    }
}