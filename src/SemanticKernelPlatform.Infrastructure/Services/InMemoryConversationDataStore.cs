using System.Collections.Concurrent;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class InMemoryConversationDataStore
    : IConversationDataStore
{
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, ConversationData>> _data =
        new(StringComparer.Ordinal);

    public Task<ConversationData?> GetAsync(
        string conversationId,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateArguments(conversationId, operationName);

        if (_data.TryGetValue(conversationId, out var operations) &&
            operations.TryGetValue(operationName, out var data))
        {
            return Task.FromResult<ConversationData?>(data);
        }

        return Task.FromResult<ConversationData?>(null);
    }

    public Task SetAsync(
        string conversationId,
        ConversationData data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(data);

        var operations = _data.GetOrAdd(
            conversationId,
            _ => new ConcurrentDictionary<string, ConversationData>(
                StringComparer.Ordinal));

        operations[data.OperationName] = data;

        return Task.CompletedTask;
    }

    public Task ClearAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        _data.TryRemove(conversationId, out _);

        return Task.CompletedTask;
    }

    private static void ValidateArguments(
        string conversationId,
        string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
    }
}