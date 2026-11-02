
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Application.Interfaces;

public interface IIntentExtractionService
{
    Task<UserIntent> ExtractIntentAsync(
        string userMessage,
        IReadOnlyList<ConversationMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}
