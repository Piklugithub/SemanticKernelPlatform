
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SemanticKernelPlatform.Infrastructure.Models.Ollama;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class OllamaToolCallingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public OllamaToolCallingClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _modelId = configuration["AI:Ollama:ModelId"]
            ?? throw new InvalidOperationException(
                "AI:Ollama:ModelId is not configured.");
    }

    public async Task<OllamaChatResponse> SendAsync(
        OllamaChatRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Model = _modelId;

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            JsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException(
            "Ollama returned an empty response.");
    }
}
