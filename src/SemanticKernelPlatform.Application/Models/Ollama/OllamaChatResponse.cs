
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticKernelPlatform.Infrastructure.Models.Ollama;

public sealed class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("message")]
    public OllamaResponseMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public sealed class OllamaResponseMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OllamaToolCall> ToolCalls { get; set; } = [];
}

public sealed class OllamaToolCall
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("function")]
    public OllamaFunctionCall? Function { get; set; }
}

public sealed class OllamaFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}
