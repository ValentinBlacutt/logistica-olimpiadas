namespace AuthService.Models;

public class UsuarioResponseDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;

    public static UsuarioResponseDto FromUsuario(Usuario usuario) => new()
    {
        Id = usuario.Id,
        Email = usuario.Email,
        Rol = usuario.Rol.Nombre
    };
}