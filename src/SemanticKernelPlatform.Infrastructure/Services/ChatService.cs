
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;
    private readonly IApiResponseContext _apiResponseContext;
    private readonly ApiResponseProcessor _apiResponseProcessor;

    public ChatService(
        Kernel kernel,
        IConfiguration configuration,
        IApiResponseContext apiResponseContext,
        ApiResponseProcessor apiResponseProcessor)
    {
        _kernel = kernel;
        _configuration = configuration;
        _apiResponseContext = apiResponseContext;
        _apiResponseProcessor = apiResponseProcessor;

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

        // Clear previous API response before processing a new message.
        _apiResponseContext.Clear();

        var history = new ChatHistory();

        var integrationInstructions = GetIntegrationInstructions();

        history.AddSystemMessage($"""
            You are an intelligent enterprise AI assistant.

            Your responsibilities:
            1. Understand user requests using natural language.
            2. Use registered plugins when external API data is required.
            3. Never ask users for internal plugin or function names.
            4. Never invent API results or business information.
            5. Use actual API responses when answering questions.
            6. If an API call fails, explain the failure honestly.
            7. Present information in clear, natural language.

            When retrieving records from an API:
            - Select the appropriate registered API function.
            - Do not invent, modify or replace API records.
            - Do not fabricate names, IDs, email addresses or other fields.

            Integration-specific business instructions:

            {integrationInstructions}
            """);

        history.AddUserMessage(request.Message);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // Let Semantic Kernel understand the request and execute
        // the appropriate registered plugin.
        var response = await _chatCompletion.GetChatMessageContentAsync(
            history,
            executionSettings: settings,
            kernel: _kernel,
            cancellationToken: cancellationToken);

        // Check whether an API plugin captured actual JSON data.
        var rawApiResponse = _apiResponseContext.RawResponse;

        if (!string.IsNullOrWhiteSpace(rawApiResponse))
        {
            var operationName = _apiResponseContext.OperationName;

            var title = GetTableTitle(operationName);

            // Convert the original API JSON into structured table data.
            var table = _apiResponseProcessor.CreateTable(
                rawApiResponse,
                title);

            if (table is not null)
            {
                return new ChatResponse
                {
                    Reply = $"Successfully retrieved {table.TotalRecords} records.",
                    ResponseType = "Table",
                    Table = table
                };
            }
        }

        // If no table data was captured, return the normal AI response.
        return new ChatResponse
        {
            Reply = response.Content ?? string.Empty,
            ResponseType = "Text"
        };
    }

    private string GetTableTitle(string? operationName)
    {
        return operationName switch
        {
            "GetUsers" => "Registered Users",
            "GetGroups" => "Ticket Management Groups",
            "GetTicketStatusCounts" => "Ticket Status Counts",
            _ => "API Results"
        };
    }

    private string GetIntegrationInstructions()
    {
        var integrations = _configuration
            .GetSection("ApiIntegrations")
            .GetChildren();

        var instructions = integrations
            .Select(integration =>
            {
                var description = integration["SystemInstructions"];

                if (string.IsNullOrWhiteSpace(description))
                    return null;

                return $"Integration: {integration.Key}\n{description}";
            })
            .OfType<string>();

        return string.Join(
            "\n\n",
            instructions);
    }
}
