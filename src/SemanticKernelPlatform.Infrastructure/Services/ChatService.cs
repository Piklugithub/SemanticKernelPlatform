
using System.Text.RegularExpressions;
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
    private readonly UserSearchProcessor _userSearchProcessor;
    private readonly TicketCountProcessor _ticketCountProcessor;

    public ChatService(
        Kernel kernel,
        IConfiguration configuration,
        IApiResponseContext apiResponseContext,
        ApiResponseProcessor apiResponseProcessor,
        UserSearchProcessor userSearchProcessor,
        TicketCountProcessor ticketCountProcessor)
    {
        _kernel = kernel;
        _configuration = configuration;
        _apiResponseContext = apiResponseContext;
        _apiResponseProcessor = apiResponseProcessor;
        _userSearchProcessor = userSearchProcessor;
        _ticketCountProcessor = ticketCountProcessor;

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

            Integration-specific business instructions:

            {integrationInstructions}
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

        // Process actual API data when GetUsers was invoked.
        var processedResponse = ProcessApiResponse(request.Message);

        if (processedResponse is not null)
        {
            return processedResponse;
        }

        // Default response when no deterministic processing is required.
        return new ChatResponse
        {
            Reply = response.Content ?? string.Empty,
            ResponseType = "Text"
        };
    }


    private ChatResponse? ProcessApiResponse(string userMessage)
    {
        var operationName = _apiResponseContext.OperationName;
        var rawResponse = _apiResponseContext.RawResponse;

        if (string.IsNullOrWhiteSpace(operationName) ||
            string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        // Process user records.
        if (string.Equals(
                operationName,
                "GetUsers",
                StringComparison.OrdinalIgnoreCase))
        {
            var usersTable = _apiResponseProcessor.CreateTable(
                rawResponse,
                "Registered Users");

            if (usersTable is null)
                return null;

            if (TryExtractUserSearchTerm(userMessage, out var searchTerm))
            {
                return _userSearchProcessor.SearchUsers(
                    usersTable,
                    searchTerm);
            }

            return new ChatResponse
            {
                Reply = $"Successfully retrieved {usersTable.TotalRecords} records.",
                ResponseType = "Table",
                Table = usersTable
            };
        }

        // Process actual ticket counts.
        if (string.Equals(
                operationName,
                "GetTicketStatusCounts",
                StringComparison.OrdinalIgnoreCase))
        {
            var ticketTable = _apiResponseProcessor.CreateTable(
                rawResponse,
                "Ticket Counts by Status");

            if (ticketTable is null)
                return null;

            // Return the actual count when a particular status is requested.
            if (TryExtractTicketStatus(userMessage, out var status))
            {
                return _ticketCountProcessor.GetCountByStatus(
                    ticketTable,
                    status);
            }

            // Otherwise, return the actual status/count table.
            return new ChatResponse
            {
                Reply = $"Successfully retrieved ticket counts. Total tickets: {ticketTable.Rows.Sum(row => int.TryParse(row.LastOrDefault(), out var count) ? count : 0)}.",
                ResponseType = "Table",
                Table = ticketTable
            };
        }

        return null;
    }




    private static bool TryExtractUserSearchTerm(
        string message,
        out string searchTerm)
    {
        searchTerm = string.Empty;

        var patterns = new[]
        {
        // Is Sayan registered?
        @"^\s*(?:is|are)\s+(?<name>.+?)\s+(?:registered|a registered user)\s*[?.!]*\s*$",

        // Find Sayan / Find Sayan details
        // Search for Sayan
        @"^\s*(?:find|search for|look for)\s+(?:users?\s+(?:named|called)\s+)?(?<name>.+?)\s*[?.!]*\s*$",

        // Show me users named Sayan
        @"^\s*(?:show|list)\s+(?:me\s+)?users?\s+(?:named|called)\s+(?<name>.+?)\s*[?.!]*\s*$",

        // Get me the details of Sayan
        // Give me information about Sayan
        @"^\s*(?:get|give|show|tell)\s+(?:me\s+)?(?:the\s+)?(?:details|information|info|profile)\s+(?:of|for|about)\s+(?<name>.+?)\s*[?.!]*\s*$"
    };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(
                message,
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            if (!match.Success)
                continue;

            searchTerm = match.Groups["name"].Value.Trim();

            searchTerm = Regex.Replace(
                searchTerm,
                @"\s+(?:details|information|info|records|profile)$",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant).Trim();

            return !string.IsNullOrWhiteSpace(searchTerm);
        }

        return false;
    }



    private static bool TryExtractTicketStatus(
        string message,
        out string status)
    {
        status = string.Empty;

        const string statusPattern =
            @"(?<status>open|active|resolved|on[\s-]*hold)";

        var patterns = new[]
        {
        // How many tickets are currently open?
        // How many tickets are open?
        $@"^\s*how many\s+tickets?\s+(?:are\s+)?(?:currently\s+)?{statusPattern}\s*[?.!]*\s*$",

        // How many open tickets?
        // How many active tickets?
        $@"^\s*how many\s+{statusPattern}\s+tickets?\s*[?.!]*\s*$",

        // How many tickets have status open?
        $@"^\s*how many\s+tickets?\s+(?:have|with)\s+(?:the\s+)?status\s+{statusPattern}\s*[?.!]*\s*$",

        // Count open tickets
        // Count of open tickets
        $@"^\s*(?:count|number)\s+(?:of\s+)?{statusPattern}\s+tickets?\s*[?.!]*\s*$"
    };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(
                message,
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            if (!match.Success)
                continue;

            status = match.Groups["status"].Value.Trim();

            return !string.IsNullOrWhiteSpace(status);
        }

        return false;
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
            .Where(instruction => instruction is not null);

        return string.Join("\n\n", instructions);
    }
}
