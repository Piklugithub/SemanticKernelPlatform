
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Application.Interfaces;

public interface IIntentExtractionService
{
    Task<UserIntent> ExtractIntentAsync(
        string userMessage,
        CancellationToken cancellationToken = default);
}
