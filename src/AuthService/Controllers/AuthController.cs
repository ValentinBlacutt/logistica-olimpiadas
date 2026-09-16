using System.Security.Claims;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly TokenService _tokenService;
    private readonly PasswordHasher<Usuario> _hasher = new();

    public AuthController(AuthDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario is null)
            return Unauthorized("Credenciales inválidas.");

        var resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized("Credenciales inválidas.");

        var (token, expiresAt) = _tokenService.GenerarToken(usuario);

        return Ok(new LoginResponse { Token = token, ExpiresAt = expiresAt });
    }

    [HttpGet("yo")]
    [Authorize]
    public IActionResult Yo()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var rol = User.FindFirst(ClaimTypes.Role)?.Value;
        return Ok(new { email, rol });
    }
}