using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Application.Interfaces;

public interface IConversationDataStore
{
    Task<ConversationData?> GetAsync(
        string conversationId,
        string operationName,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string conversationId,
        ConversationData data,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        string conversationId,
        CancellationToken cancellationToken = default);
}