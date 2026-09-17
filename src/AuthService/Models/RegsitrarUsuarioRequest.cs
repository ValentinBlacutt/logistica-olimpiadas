namespace AuthService.Models;

public class RegistroRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty; // "Administrador" o "Repartidor"
}