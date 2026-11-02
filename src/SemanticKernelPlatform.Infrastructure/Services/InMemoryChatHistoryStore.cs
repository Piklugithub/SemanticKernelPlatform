
using System.Collections.Concurrent;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class InMemoryChatHistoryStore : IChatHistoryStore
{
    private readonly ConcurrentDictionary<
        string,
        ConcurrentQueue<ConversationMessage>> _conversations =
        new(StringComparer.Ordinal);

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException(
                "Conversation ID is required.",
                nameof(conversationId));
        }

        if (!_conversations.TryGetValue(
                conversationId,
                out var messages))
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(
                Array.Empty<ConversationMessage>());
        }

        IReadOnlyList<ConversationMessage> result =
            messages.ToArray();

        return Task.FromResult(result);
    }

    public Task AddMessageAsync(
        string conversationId,
        ConversationMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException(
                "Conversation ID is required.",
                nameof(conversationId));
        }

        ArgumentNullException.ThrowIfNull(message);

        var messages = _conversations.GetOrAdd(
            conversationId,
            _ => new ConcurrentQueue<ConversationMessage>());

        messages.Enqueue(message);

        return Task.CompletedTask;
    }

    public Task ClearConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException(
                "Conversation ID is required.",
                nameof(conversationId));
        }

        _conversations.TryRemove(conversationId, out _);

        return Task.CompletedTask;
    }
}
