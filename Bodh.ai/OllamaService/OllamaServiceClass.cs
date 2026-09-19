using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Net.Http.Json;

public interface IOllamaService
{
    IAsyncEnumerable<string> SendMessageStreamAsync(string userMessage);
    Task<float[]> GetEmbeddingAsync(string text, string model = "nomic-embed-text");
}

public class OllamaServiceClass : IOllamaService
{
    private readonly OllamaApiClient _ollama;
    private readonly Chat _chat;
    private readonly HttpClient _httpClient;

    public OllamaServiceClass(string endpoint = "http://localhost:11434", string model = "qwen3.5:4b")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/") };
        _ollama = new OllamaApiClient(new Uri(endpoint))
        {
            SelectedModel = model
        };
        _chat = new Chat(_ollama);
    }

    // Developer code: this method centralizes the data-access contract with the local Ollama API so the UI remains focused on presentation.
    public async IAsyncEnumerable<string> SendMessageStreamAsync(string userMessage)
    {
        await foreach (var token in _chat.SendAsync(userMessage))
        {
            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }

    // Agent code: keep HTTP calls isolated here to make retries, error handling, and model updates easier to test later.
    public async Task<float[]> GetEmbeddingAsync(string text, string model = "nomic-embed-text")
    {
        var payload = new { model = model, prompt = text };
        var response = await _httpClient.PostAsJsonAsync("api/embeddings", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }

    private record OllamaEmbeddingResponse(float[] Embedding);
}