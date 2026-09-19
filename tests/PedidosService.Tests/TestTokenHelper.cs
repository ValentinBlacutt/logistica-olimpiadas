using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PedidosService.Tests;

public static class TestTokenHelper
{
    // Debe coincidir EXACTO con Jwt:Key y Jwt:Issuer de appsettings.json
    // de PedidosService — son los mismos valores que usa AuthService.
    private const string Key = "CLAVE-SECRETA-DE-AL-MENOS-32-CARACTERES-CAMBIAR-EN-PROD";
    private const string Issuer = "LogisticaAuthService";

    public static string GenerarToken(int usuarioId, string email, string rol, int? repartidorId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, rol)
        };

        if (repartidorId.HasValue)
            claims.Add(new Claim("repartidor_id", repartidorId.Value.ToString()));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}