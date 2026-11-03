namespace SemanticKernelPlatform.Application.Models;

public sealed record ConversationData(
    string OperationName,
    string RawResponse,
    DateTime RetrievedAtUtc);