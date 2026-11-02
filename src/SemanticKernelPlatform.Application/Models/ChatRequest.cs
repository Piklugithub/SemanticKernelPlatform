
namespace SemanticKernelPlatform.Application.Models;

public sealed record ChatRequest(
    string Message,
    string? ConversationId = null);
