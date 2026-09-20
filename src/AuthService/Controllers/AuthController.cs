using System.Security.Claims;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
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
    public async Task<ActionResult<LoginResponse>> Login(AuthService.Models.LoginRequest request)
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

    [HttpPost("registro")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<UsuarioResponseDto>> Registro(RegistroRequest request)
    {
        var existe = await _db.Usuarios.AnyAsync(u => u.Email == request.Email);
        if (existe)
            return Conflict("Ya existe un usuario con ese email.");

        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == request.Rol);
        if (rol is null)
            return BadRequest($"El rol '{request.Rol}' no existe. Roles válidos: Administrador, Repartidor.");

        var usuario = new Usuario
        {
            Email = request.Email,
            RolId = rol.Id
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, request.Password);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        usuario.Rol = rol; // ya lo tenemos en memoria, evita un round-trip a la DB

        return CreatedAtAction(nameof(Yo), UsuarioResponseDto.FromUsuario(usuario));
    }

    [HttpGet("repartidores")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ListarRepartidores()
    {
        var repartidores = await _db.Usuarios
            .Include(u => u.Rol)
            .Where(u => u.Rol.Nombre == "Repartidor")
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();

        return Ok(repartidores);
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