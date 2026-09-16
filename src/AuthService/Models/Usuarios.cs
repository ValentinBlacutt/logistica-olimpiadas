namespace AuthService.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;

}