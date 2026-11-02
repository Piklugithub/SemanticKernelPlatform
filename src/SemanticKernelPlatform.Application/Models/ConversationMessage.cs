namespace SemanticKernelPlatform.Application.Models;

public sealed record ConversationMessage(
    string Role,
    string Content,
    DateTime CreatedAt);