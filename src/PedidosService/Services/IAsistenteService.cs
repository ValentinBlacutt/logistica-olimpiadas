using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PedidosService.Services;


public interface IAsistenteService
{
    Task<string> ConsultarAsync(string pregunta, string contextoDatos);
    IAsyncEnumerable<string> ConsultarStreamAsync(string pregunta, string contextoDatos, CancellationToken cancellationToken = default);
}

