using AuthService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests;

public class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "AuthTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptores = services
                .Where(d => d.ServiceType.Namespace != null
                    && d.ServiceType.Namespace.StartsWith("Microsoft.EntityFrameworkCore"))
                .ToList();

            foreach (var descriptor in descriptores)
                services.Remove(descriptor);

            services.AddDbContext<AuthDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}