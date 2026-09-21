using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PedidosService.Services;

public class AsistenteIAService : IAsistenteService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AsistenteIAService> _logger;
    private readonly string _apiKey;
    private readonly string _modeloConfigurado;

    public AsistenteIAService(HttpClient httpClient, IConfiguration configuration, ILogger<AsistenteIAService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["Ia:ApiKey"]
                  ?? configuration["Groq:ApiKey"]
                  ?? throw new ArgumentNullException("No se encontró la ApiKey de Groq en appsettings.json (Ia:ApiKey)");

        _modeloConfigurado = configuration["Ia:Modelo"]
                            ?? configuration["Groq:Modelo"]
                            ?? "openai/gpt-oss-20b";

        // Autenticación Bearer Token requerida por Groq
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    // Consulta tradicional (bloqueante/síncrona)
    public async Task<string> ConsultarAsync(string pregunta, string contextoDatos)
    {
        var requestBody = ConstruirPayload(_modeloConfigurado, pregunta, contextoDatos, stream: false);
        var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Error en la API de Groq: {StatusCode} — {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Error al consultar Groq: {response.StatusCode}");
        }

        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = doc?.RootElement;

        return root?.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
               ?? "No se pudo obtener una respuesta válida.";
    }

    // Consulta por Streaming Server-Sent Events (SSE)
    public async IAsyncEnumerable<string> ConsultarStreamAsync(
        string pregunta,
        string contextoDatos,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = ConstruirPayload(_modeloConfigurado, pregunta, contextoDatos, stream: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error en Stream de Groq: {StatusCode} — {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Error en Stream de Groq: {response.StatusCode}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var jsonChunk = line["data: ".Length..].Trim();
            if (jsonChunk == "[DONE]") break;

            string? textoProcesado = null;
            try
            {
                using var doc = JsonDocument.Parse(jsonChunk);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var contentProp))
                    {
                        textoProcesado = contentProp.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Ignorar fragmentos JSON malformados
            }

            if (!string.IsNullOrEmpty(textoProcesado))
            {
                yield return textoProcesado;
            }
        }
    }

    private static object ConstruirPayload(string modelo, string pregunta, string contextoDatos, bool stream) => new
    {
        model = modelo,
        stream = stream,
        messages = new[]
     {
        new
        {
            role = "system",
            content = "Sos un asistente exclusivo de logística y gestión de pedidos. " +
                      "Responde ÚNICAMENTE sobre el estado, detalles y datos operativos de los pedidos provistos en el contexto. " +
                      "Si el usuario pregunta sobre temas ajenos (deportes, noticias, historia, temas generales u otros personajes), " +
                      "rechaza amablemente la solicitud indicando que solo podés responder sobre la gestión de pedidos."
        },
        new
        {
            role = "user",
            content = $"DATOS:\n{contextoDatos}\n\nPREGUNTA: {pregunta}"
        }
    },
        temperature = 0.1, // Temperatura baja para evitar alucinaciones
        max_tokens = 400
    };
}