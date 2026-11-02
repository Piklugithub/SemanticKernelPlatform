
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class ChatService : IChatService
{
    private const string PluginName = "HttpApi";

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IApiResponseContext _apiResponseContext;
    private readonly ApiResponseProcessor _apiResponseProcessor;
    private readonly UserSearchProcessor _userSearchProcessor;
    private readonly TicketCountProcessor _ticketCountProcessor;
    private readonly TicketAnalysisProcessor _ticketAnalysisProcessor;
    private readonly IIntentExtractionService _intentExtractionService;
    private readonly IChatHistoryStore _chatHistoryStore;

    public ChatService(
        Kernel kernel,
        IApiResponseContext apiResponseContext,
        ApiResponseProcessor apiResponseProcessor,
        UserSearchProcessor userSearchProcessor,
        TicketCountProcessor ticketCountProcessor,
        TicketAnalysisProcessor ticketAnalysisProcessor,
        IIntentExtractionService intentExtractionService,
        IChatHistoryStore chatHistoryStore)
    {
        _kernel = kernel;
        _apiResponseContext = apiResponseContext;
        _apiResponseProcessor = apiResponseProcessor;
        _userSearchProcessor = userSearchProcessor;
        _ticketCountProcessor = ticketCountProcessor;
        _ticketAnalysisProcessor = ticketAnalysisProcessor;
        _intentExtractionService = intentExtractionService;
        _chatHistoryStore = chatHistoryStore;

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

        // Step 1: Resolve the conversation ID.
        var conversationId = string.IsNullOrWhiteSpace(
            request.ConversationId)
                ? Guid.NewGuid().ToString("N")
                : request.ConversationId;

        // Step 2: Load previous conversation messages.
        var previousMessages =
            await _chatHistoryStore.GetMessagesAsync(
                conversationId,
                cancellationToken);

        // Prevent data from a previous API request being reused.
        _apiResponseContext.Clear();

        // Step 3: Understand the user's intent.
        // Context-aware intent extraction will be added in the next step.
        var intent = await _intentExtractionService.ExtractIntentAsync(
            request.Message,
            previousMessages,
            cancellationToken);

        // Step 4: Execute the appropriate operation.
        ChatResponse response;

        switch (intent.Intent)
        {
            case IntentType.GetAllUsers:
            case IntentType.SearchUsers:

                response = await ExecuteApiOperationAsync(
                    "GetUsers",
                    intent,
                    cancellationToken);
                break;

            case IntentType.GetTicketCount:
            case IntentType.GetTicketCountsByStatus:
            case IntentType.AnalyzeTicketDistribution:

                response = await ExecuteApiOperationAsync(
                    "GetTicketStatusCounts",
                    intent,
                    cancellationToken);
                break;

            case IntentType.GetGroups:

                response = await ExecuteApiOperationAsync(
                    "GetGroups",
                    intent,
                    cancellationToken);
                break;

            case IntentType.Unknown:
            default:

                response = await HandleUnknownIntentAsync(
                    request.Message,
                    previousMessages,
                    cancellationToken);
                break;
        }

        // Step 5: Attach the conversation ID to the response.
        response.ConversationId = conversationId;

        // Step 6: Save the current user and assistant messages.
        await SaveConversationMessagesAsync(
            conversationId,
            request.Message,
            response.Reply,
            cancellationToken);

        return response;
    }

    private async Task SaveConversationMessagesAsync(
        string conversationId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken)
    {
        await _chatHistoryStore.AddMessageAsync(
            conversationId,
            new ConversationMessage(
                "user",
                userMessage,
                DateTime.UtcNow),
            cancellationToken);

        await _chatHistoryStore.AddMessageAsync(
            conversationId,
            new ConversationMessage(
                "assistant",
                assistantMessage,
                DateTime.UtcNow),
            cancellationToken);
    }

    private async Task<ChatResponse> ExecuteApiOperationAsync(
        string functionName,
        UserIntent intent,
        CancellationToken cancellationToken)
    {
        var function = _kernel.Plugins.GetFunction(
            PluginName,
            functionName);

        var functionResult = await _kernel.InvokeAsync(
            function,
            cancellationToken: cancellationToken);

        var rawResponse = _apiResponseContext.RawResponse;
        var operationName = _apiResponseContext.OperationName;

        if (string.IsNullOrWhiteSpace(rawResponse) ||
            string.IsNullOrWhiteSpace(operationName))
        {
            return new ChatResponse
            {
                Reply = functionResult.ToString(),
                ResponseType = "Text"
            };
        }

        return await ProcessApiResponseAsync(
            intent,
            operationName,
            rawResponse,
            cancellationToken);
    }

    private async Task<ChatResponse> ProcessApiResponseAsync(
        UserIntent intent,
        string operationName,
        string rawResponse,
        CancellationToken cancellationToken)
    {
        switch (operationName)
        {
            case "GetUsers":
                {
                    var usersTable = _apiResponseProcessor.CreateTable(
                        rawResponse,
                        "Registered Users");

                    if (usersTable is null)
                    {
                        return CreateErrorResponse(
                            "Unable to process the registered users response.");
                    }

                    if (intent.Intent == IntentType.SearchUsers)
                    {
                        if (string.IsNullOrWhiteSpace(intent.SearchTerm))
                        {
                            return CreateErrorResponse(
                                "I couldn't identify the user's name to search for.");
                        }

                        return _userSearchProcessor.SearchUsers(
                            usersTable,
                            intent.SearchTerm);
                    }

                    return new ChatResponse
                    {
                        Reply = $"Successfully retrieved {usersTable.TotalRecords} registered users.",
                        ResponseType = "Table",
                        Table = usersTable
                    };
                }

            case "GetTicketStatusCounts":
                {
                    var ticketTable = _apiResponseProcessor.CreateTable(
                        rawResponse,
                        "Ticket Counts by Status");

                    if (ticketTable is null)
                    {
                        return CreateErrorResponse(
                            "Unable to process the ticket count response.");
                    }

                    if (intent.Intent == IntentType.GetTicketCount)
                    {
                        if (string.IsNullOrWhiteSpace(intent.Status))
                        {
                            return CreateErrorResponse(
                                "I couldn't identify the ticket status.");
                        }

                        return _ticketCountProcessor.GetCountByStatus(
                            ticketTable,
                            intent.Status);
                    }

                    if (intent.Intent == IntentType.AnalyzeTicketDistribution)
                    {
                        var analysis = _ticketAnalysisProcessor.Analyze(
                            ticketTable);

                        return await GenerateTicketAnalysisAsync(
                            analysis,
                            cancellationToken);
                    }

                    var totalTickets = ticketTable.Rows.Sum(row =>
                        int.TryParse(
                            row.LastOrDefault(),
                            out var count)
                                ? count
                                : 0);

                    return new ChatResponse
                    {
                        Reply = $"Successfully retrieved ticket counts. Total tickets: {totalTickets}.",
                        ResponseType = "Table",
                        Table = ticketTable
                    };
                }

            case "GetGroups":
                {
                    var groupsTable = _apiResponseProcessor.CreateTable(
                        rawResponse,
                        "Registered Groups");

                    if (groupsTable is null)
                    {
                        return CreateErrorResponse(
                            "Unable to process the groups response.");
                    }

                    return new ChatResponse
                    {
                        Reply = $"Successfully retrieved {groupsTable.TotalRecords} groups.",
                        ResponseType = "Table",
                        Table = groupsTable
                    };
                }

            default:
                return CreateErrorResponse(
                    $"Unsupported API operation: {operationName}");
        }
    }

    private async Task<ChatResponse> HandleUnknownIntentAsync(
        string userMessage,
        IReadOnlyList<ConversationMessage> previousMessages,
        CancellationToken cancellationToken)
    {
        var history = new ChatHistory();

        history.AddSystemMessage("""
            You are an intelligent enterprise assistant.

            You currently support these business operations:
            - Retrieving registered users.
            - Searching registered users.
            - Retrieving ticket counts.
            - Retrieving ticket counts by status.
            - Analyzing ticket distribution.
            - Retrieving registered groups.

            Use previous conversation messages when they are relevant
            to understanding the current question.

            If the user asks about something outside these capabilities,
            politely explain the supported capabilities.

            Never invent business information or API results.
            Do not claim that an API operation was executed.
            """);

        // Reconstruct the previous conversation.
        foreach (var message in previousMessages)
        {
            switch (message.Role.ToLowerInvariant())
            {
                case "user":
                    history.AddUserMessage(message.Content);
                    break;

                case "assistant":
                    history.AddAssistantMessage(message.Content);
                    break;
            }
        }

        // Add the current user message.
        history.AddUserMessage(userMessage);

        var response = await _chatCompletion.GetChatMessageContentAsync(
            history,
            cancellationToken: cancellationToken);

        return new ChatResponse
        {
            Reply = response.Content ?? string.Empty,
            ResponseType = "Text"
        };
    }

    private async Task<ChatResponse> GenerateTicketAnalysisAsync(
        TicketAnalysisSummary analysis,
        CancellationToken cancellationToken)
    {
        var analysisJson = JsonSerializer.Serialize(
            analysis,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        var history = new ChatHistory();

        history.AddSystemMessage("""
            You are an enterprise business data analyst.

            Your responsibility is to explain calculated business metrics.

            Rules:
            1. Use only the supplied analysis data.
            2. Never invent or modify ticket counts or percentages.
            3. Identify the highest-volume ticket status.
            4. Identify the lowest-volume ticket status.
            5. Explain the overall ticket distribution.
            6. Mention important observations.
            7. Do not assume that a high or low count indicates
               a business problem without supporting context.
            8. Keep the explanation concise and professional.
            9. Do not return JSON or Markdown code fences.
            """);

        history.AddUserMessage($"""
            Analyze the following calculated ticket metrics.

            Analysis data:
            {analysisJson}

            Provide an executive summary of the ticket distribution.
            """);

        var response = await _chatCompletion.GetChatMessageContentAsync(
            history,
            cancellationToken: cancellationToken);

        return new ChatResponse
        {
            Reply = response.Content ?? string.Empty,
            ResponseType = "Analysis",
            Analysis = analysis
        };
    }

    private static ChatResponse CreateErrorResponse(string message)
    {
        return new ChatResponse
        {
            Reply = message,
            ResponseType = "Text"
        };
    }
}
