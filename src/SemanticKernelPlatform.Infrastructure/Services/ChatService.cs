
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;
using SemanticKernelPlatform.Infrastructure.Models.Ollama;

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
    private readonly IConversationDataStore _conversationDataStore;
    private readonly OllamaToolCallingClient _ollamaToolCallingClient;
    private static readonly TimeSpan CacheDuration =
    TimeSpan.FromMinutes(5);

    public ChatService(
        Kernel kernel,
        IApiResponseContext apiResponseContext,
        ApiResponseProcessor apiResponseProcessor,
        UserSearchProcessor userSearchProcessor,
        TicketCountProcessor ticketCountProcessor,
        TicketAnalysisProcessor ticketAnalysisProcessor,
        IIntentExtractionService intentExtractionService,
        IChatHistoryStore chatHistoryStore,
        IConversationDataStore conversationDataStore,
        OllamaToolCallingClient ollamaToolCallingClient)
    {
        _kernel = kernel;
        _apiResponseContext = apiResponseContext;
        _apiResponseProcessor = apiResponseProcessor;
        _userSearchProcessor = userSearchProcessor;
        _ticketCountProcessor = ticketCountProcessor;
        _ticketAnalysisProcessor = ticketAnalysisProcessor;
        _intentExtractionService = intentExtractionService;
        _chatHistoryStore = chatHistoryStore;
        _conversationDataStore = conversationDataStore;
        _ollamaToolCallingClient = ollamaToolCallingClient;

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
            case IntentType.GetGroups:
            case IntentType.GetTicketCount:
            case IntentType.GetTicketCountsByStatus:
            case IntentType.AnalyzeTicketDistribution:

                response = await ExecuteToolCallingAsync(
                    request.Message,
                    intent,
                    conversationId,
                    cancellationToken);
                break;

            case IntentType.SearchUsers:

                response = await ExecuteApiOperationAsync(
                    "GetUsers",
                    intent,
                    conversationId,
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
        string conversationId,
        CancellationToken cancellationToken)
    {
        // Step 1: Retrieve cached data unless cache is bypassed.
        ConversationData? cachedData = null;

        if (intent.CachePolicy != CachePolicy.Bypass)
        {
            cachedData = await _conversationDataStore.GetAsync(
                conversationId,
                functionName,
                cancellationToken);
        }

        // Step 2: Return valid cached data when UseCache is selected.
        if (intent.CachePolicy == CachePolicy.UseCache &&
            cachedData is not null &&
            IsCacheValid(cachedData))
        {
            return await ProcessApiResponseAsync(
                intent,
                cachedData.OperationName,
                cachedData.RawResponse,
                cancellationToken);
        }

        // Step 3: Cache miss, expired data, or Refresh policy.
        // Fetch fresh data from the actual API.
        _apiResponseContext.Clear();

        var function = _kernel.Plugins.GetFunction(
            PluginName,
            functionName);

        var functionResult = await _kernel.InvokeAsync(
            function,
            cancellationToken: cancellationToken);

        var rawResponse = _apiResponseContext.RawResponse;
        var operationName = _apiResponseContext.OperationName;

        // Step 4: Handle API failures or missing response data.
        if (string.IsNullOrWhiteSpace(rawResponse) ||
            string.IsNullOrWhiteSpace(operationName))
        {
            return new ChatResponse
            {
                Reply = functionResult.ToString(),
                ResponseType = "Text"
            };
        }

        // Step 5: Store successful API response unless cache is bypassed.
        if (intent.CachePolicy != CachePolicy.Bypass)
        {
            await _conversationDataStore.SetAsync(
                conversationId,
                new ConversationData(
                    operationName,
                    rawResponse,
                    DateTime.UtcNow),
                cancellationToken);
        }

        // Step 6: Process the API response.
        return await ProcessApiResponseAsync(
            intent,
            operationName,
            rawResponse,
            cancellationToken);
    }


    private static bool IsCacheValid(ConversationData data)
    {
        var age = DateTime.UtcNow - data.RetrievedAtUtc;

        return age >= TimeSpan.Zero &&
               age < CacheDuration;
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





    private async Task<ChatResponse> ExecuteToolCallingAsync(
        string userMessage,
        UserIntent intent,
        string conversationId,
        CancellationToken cancellationToken)
    {
        // Step 1: Ask Ollama to select an available tool.
        var request = new OllamaChatRequest
        {
            Stream = false,
            Messages =
            [
                new OllamaMessage
            {
                Role = "user",
                Content = userMessage
            }
            ],
            Tools = OllamaToolCatalog.GetTools().ToList()
        };

        var ollamaResponse = await _ollamaToolCallingClient.SendAsync(
            request,
            cancellationToken);

        var toolCalls = ollamaResponse.Message?.ToolCalls;

        if (toolCalls is null || toolCalls.Count != 1)
        {
            return CreateErrorResponse(
                "The AI assistant did not return exactly one supported tool call.");
        }

        // Step 2: Map the selected Ollama tool to a Semantic Kernel function.
        var selectedTool = toolCalls[0].Function?.Name;

        var functionName = selectedTool?.ToLowerInvariant() switch
        {
            "get_users" => "GetUsers",
            "get_groups" => "GetGroups",
            "get_ticket_status_counts" => "GetTicketStatusCounts",
            _ => null
        };

        if (functionName is null)
        {
            return CreateErrorResponse(
                $"Unsupported tool requested by Ollama: {selectedTool}");
        }

        // Step 3: Ensure the selected tool is appropriate for the intent.
        var expectedFunctionName = intent.Intent switch
        {
            IntentType.GetAllUsers => "GetUsers",
            IntentType.GetGroups => "GetGroups",

            IntentType.GetTicketCount or
            IntentType.GetTicketCountsByStatus or
            IntentType.AnalyzeTicketDistribution
                => "GetTicketStatusCounts",

            _ => null
        };

        if (!string.Equals(
                functionName,
                expectedFunctionName,
                StringComparison.Ordinal))
        {
            return CreateErrorResponse(
                "The selected tool does not match the requested business operation.");
        }

        // Step 4: Execute the existing Semantic Kernel workflow.
        var apiResult = await ExecuteApiOperationAsync(
            functionName,
            intent,
            conversationId,
            cancellationToken);

        // Step 5: Build a concise tool result.
        var toolResult = BuildToolResult(apiResult);

        // If the API operation failed, return the original result.
        if (toolResult is null)
        {
            return apiResult;
        }

        // Step 6: Return the assistant's original tool call and its result.
        var followUpRequest = new OllamaChatRequest
        {
            Stream = false,
            Messages =
            [
                new OllamaMessage
            {
                Role = "user",
                Content = userMessage
            },
            new OllamaMessage
            {
                Role = "assistant",
                Content = ollamaResponse.Message?.Content ?? string.Empty,
                ToolCalls = toolCalls
            },
            new OllamaMessage
            {
                Role = "tool",
                ToolName = selectedTool,
                Content = toolResult
            }
            ],
            Tools = OllamaToolCatalog.GetTools().ToList()
        };

        // Step 7: Generate the final response.
        var finalResponse = await _ollamaToolCallingClient.SendAsync(
            followUpRequest,
            cancellationToken);

        var finalReply = finalResponse.Message?.Content;

        if (string.IsNullOrWhiteSpace(finalReply))
        {
            return apiResult;
        }

        apiResult.Reply = finalReply;

        return apiResult;
    }


    private static string? BuildToolResult(ChatResponse response)
    {
        // Do not ask the LLM to explain a failed API operation.
        if (response.Reply.StartsWith(
                "API_CALL_FAILED",
                StringComparison.OrdinalIgnoreCase) ||
            response.Reply.StartsWith(
                "API_RESPONSE_INVALID_JSON",
                StringComparison.OrdinalIgnoreCase) ||
            response.Reply.StartsWith(
                "Unable to connect",
                StringComparison.OrdinalIgnoreCase) ||
            response.Reply.StartsWith(
                "The API request timed out",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return response.ResponseType switch
        {
            "Table" when response.Table is not null =>
                $"""
            The requested API operation completed successfully.
            Table title: {response.Table.Title}
            Total records: {response.Table.TotalRecords}
            The application has prepared the complete structured table.
            Do not invent or reproduce individual records.
            """,

            "Analysis" when response.Analysis is not null =>
                $"""
            The ticket analysis operation completed successfully.

            Calculated analysis:
            {JsonSerializer.Serialize(response.Analysis)}

            Use only the supplied analysis values.
            Do not invent or modify business metrics.
            """,

            "Text" when !string.IsNullOrWhiteSpace(response.Reply) =>
                $"""
            The requested business operation completed.

            Result:
            {response.Reply}
            """,

            _ => null
        };
    }


}
