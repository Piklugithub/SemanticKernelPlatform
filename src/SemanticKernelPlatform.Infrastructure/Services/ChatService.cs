
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;

    public ChatService(Kernel kernel)
    {
        _kernel = kernel;

        _chatCompletion = _kernel.Services
            .GetRequiredService<IChatCompletionService>();
    }


    public async Task<ChatResponse> SendMessageAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException(
                "Message is required.",
                nameof(request));
        }

        var history = new ChatHistory();

        history.AddSystemMessage("""
        You are a helpful AI platform assistant.

        Use registered plugins when they provide relevant information
        or functionality. Never claim that a plugin performed an
        operation unless it actually did so.
        """);

        history.AddUserMessage(request.Message);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var response = await _chatCompletion.GetChatMessageContentAsync(
            history,
            executionSettings: settings,
            kernel: _kernel,
            cancellationToken: cancellationToken);

        return new ChatResponse(response.Content ?? string.Empty);
    }

}
