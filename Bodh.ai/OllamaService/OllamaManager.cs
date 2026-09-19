using System.Diagnostics;
using System.Net.Http.Json;
using Bodh.ai;

public static class OllamaManager
{
    private static readonly ILoggerService _logger = LoggerService.Instance;
    private static Process? _ollamaProcess;
    private static readonly HttpClient _httpClient = new() { BaseAddress = new Uri("http://localhost:11434/") };

    // Start Ollama server programmatically (Optional)
    public static void StartServer()
    {
        try
        {
            if (_ollamaProcess != null && !_ollamaProcess.HasExited)
                return;

            _ollamaProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _ollamaProcess.Start();
            _logger.LogEvent("Ollama server started successfully.", "OLLAMA");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "OllamaManager.StartServer failed.");
        }
    }

    // 1. Cool Down: Force Ollama to immediately unload the model from GPU/VRAM
    public static async Task CoolDownModelAsync(string modelName = "qwen3.5:4b")
    {
        try
        {
            var payload = new
            {
                model = modelName,
                keep_alive = 0 // Instantly frees GPU VRAM
            };

            await _httpClient.PostAsJsonAsync("api/generate", payload);
            _logger.LogEvent($"Ollama model {modelName} cooled down.", "OLLAMA");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"OllamaManager.CoolDownModelAsync failed for {modelName}.");
        }
    }

    // 2. Shutdown: Kill the process tree completely
    public static void StopServer()
    {
        try
        {
            if (_ollamaProcess != null && !_ollamaProcess.HasExited)
            {
                _ollamaProcess.Kill(entireProcessTree: true);
                _ollamaProcess.Dispose();
                _ollamaProcess = null;
            }

            foreach (var proc in Process.GetProcessesByName("ollama"))
            {
                proc.Kill(entireProcessTree: true);
            }

            _logger.LogEvent("Ollama server stopped successfully.", "OLLAMA");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "OllamaManager.StopServer failed.");
        }
    }
}