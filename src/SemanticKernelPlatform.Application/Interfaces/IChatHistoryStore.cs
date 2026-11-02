using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Application.Interfaces;

public interface IChatHistoryStore
{
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(
        string conversationId,
        ConversationMessage message,
        CancellationToken cancellationToken = default);

    Task ClearConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);
}