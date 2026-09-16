using System.Net;
using System.Net.Http.Json;
using AuthService.Data;
using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthService.Tests;

public class AuthControllerTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        SeedUsuarioDePrueba();
    }

    private void SeedUsuarioDePrueba()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        Console.WriteLine($"[SEED] DbContext hash: {db.GetHashCode()} | Roles antes: {db.Roles.Count()} | Usuarios antes: {db.Usuarios.Count()}");

        if (!db.Roles.Any(r => r.Nombre == "Administrador"))
        {
            db.Roles.Add(new Rol { Nombre = "Administrador" });
        }
        if (!db.Roles.Any(r => r.Nombre == "Repartidor"))
        {
            db.Roles.Add(new Rol { Nombre = "Repartidor" });
        }
        db.SaveChanges();

        var adminRol = db.Roles.First(r => r.Nombre == "Administrador");

        if (!db.Usuarios.Any(u => u.Email == "admin@test.com"))
        {
            var hasher = new PasswordHasher<Usuario>();
            var admin = new Usuario { Email = "admin@test.com", RolId = adminRol.Id };
            admin.PasswordHash = hasher.HashPassword(admin, "Password123!");
            db.Usuarios.Add(admin);
            db.SaveChanges();
        }

        Console.WriteLine($"[SEED] Usuarios después: {db.Usuarios.Count()}");
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_DevuelveTokenYExpiracion()
    {
        var request = new LoginRequest { Email = "admin@test.com", Password = "Password123!" };

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_Devuelve401()
    {
        var request = new LoginRequest { Email = "admin@test.com", Password = "PasswordIncorrecta" };

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ConUsuarioInexistente_Devuelve401()
    {
        var request = new LoginRequest { Email = "noexiste@test.com", Password = "CualquierCosa123!" };

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointProtegido_SinToken_Devuelve401()
    {
        var response = await _client.GetAsync("/api/auth/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointProtegido_ConTokenValido_DevuelveEmailYRol()
    {
        var loginRequest = new LoginRequest { Email = "admin@test.com", Password = "Password123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);

        var response = await _client.GetAsync("/api/auth/yo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contenido = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin@test.com", contenido);
        Assert.Contains("Administrador", contenido);
    }
}