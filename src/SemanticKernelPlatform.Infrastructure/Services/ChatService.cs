
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
    private readonly IIntentExtractionService _intentExtractionService;

    public ChatService(
        Kernel kernel,
        IApiResponseContext apiResponseContext,
        ApiResponseProcessor apiResponseProcessor,
        UserSearchProcessor userSearchProcessor,
        TicketCountProcessor ticketCountProcessor,
        IIntentExtractionService intentExtractionService)
    {
        _kernel = kernel;
        _apiResponseContext = apiResponseContext;
        _apiResponseProcessor = apiResponseProcessor;
        _userSearchProcessor = userSearchProcessor;
        _ticketCountProcessor = ticketCountProcessor;
        _intentExtractionService = intentExtractionService;

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

        // Prevent data from a previous request being reused.
        _apiResponseContext.Clear();

        // Step 1: Understand the user's intent.
        var intent = await _intentExtractionService.ExtractIntentAsync(
            request.Message,
            cancellationToken);

        // Step 2: Execute the appropriate operation.
        switch (intent.Intent)
        {
            case IntentType.GetAllUsers:
            case IntentType.SearchUsers:

                return await ExecuteApiOperationAsync(
                    "GetUsers",
                    intent,
                    cancellationToken);

            case IntentType.GetTicketCount:
            case IntentType.GetTicketCountsByStatus:

                return await ExecuteApiOperationAsync(
                    "GetTicketStatusCounts",
                    intent,
                    cancellationToken);

            case IntentType.GetGroups:

                return await ExecuteApiOperationAsync(
                    "GetGroups",
                    intent,
                    cancellationToken);

            case IntentType.Unknown:
            default:

                return await HandleUnknownIntentAsync(
                    request.Message,
                    cancellationToken);
        }
    }

    private async Task<ChatResponse> ExecuteApiOperationAsync(
        string functionName,
        UserIntent intent,
        CancellationToken cancellationToken)
    {
        // Retrieve the registered Kernel function.
        var function = _kernel.Plugins.GetFunction(
            PluginName,
            functionName);

        // Invoke the function directly.
        // No automatic function selection or additional tool-calling
        // completion is required for these known operations.
        var functionResult = await _kernel.InvokeAsync(
            function,
            cancellationToken: cancellationToken);

        // Read the actual API response captured by HttpApiPlugin.
        var rawResponse = _apiResponseContext.RawResponse;
        var operationName = _apiResponseContext.OperationName;

        // Handle API failures or missing response data.
        if (string.IsNullOrWhiteSpace(rawResponse) ||
            string.IsNullOrWhiteSpace(operationName))
        {
            return new ChatResponse
            {
                Reply = functionResult.ToString(),
                ResponseType = "Text"
            };
        }

        return ProcessApiResponse(intent, operationName, rawResponse);
    }

    private ChatResponse ProcessApiResponse(
        UserIntent intent,
        string operationName,
        string rawResponse)
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
            - Retrieving registered groups.

            If the user asks about something outside these capabilities,
            politely explain the supported capabilities.

            Never invent business information or API results.
            Do not claim that an API operation was executed.
            """);

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

    private static ChatResponse CreateErrorResponse(string message)
    {
        return new ChatResponse
        {
            Reply = message,
            ResponseType = "Text"
        };
    }
}
