
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

    public ChatService(
        Kernel kernel,
        IConfiguration configuration,
        IApiResponseContext apiResponseContext,
        ApiResponseProcessor apiResponseProcessor,
        UserSearchProcessor userSearchProcessor)
    {
        _kernel = kernel;
        _configuration = configuration;
        _apiResponseContext = apiResponseContext;
        _apiResponseProcessor = apiResponseProcessor;
        _userSearchProcessor = userSearchProcessor;

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
        if (!string.Equals(
                _apiResponseContext.OperationName,
                "GetUsers",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rawResponse = _apiResponseContext.RawResponse;

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        var usersTable = _apiResponseProcessor.CreateTable(
            rawResponse,
            "Registered Users");

        if (usersTable is null)
        {
            return null;
        }

        // If the user requested a specific name, filter deterministically.
        if (TryExtractUserSearchTerm(userMessage, out var searchTerm))
        {
            return _userSearchProcessor.SearchUsers(
                usersTable,
                searchTerm);
        }

        // Otherwise, return all registered users.
        return new ChatResponse
        {
            Reply = $"Successfully retrieved {usersTable.TotalRecords} records.",
            ResponseType = "Table",
            Table = usersTable
        };
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

        // Find Sayan
        // Find Sayan details
        // Find users named Sayan
        // Search for Sayan details
        @"^\s*(?:find|search for|look for)\s+(?:users?\s+(?:named|called)\s+)?(?<name>.+?)\s*[?.!]*\s*$",

        // Show me users named Sayan
        // List users called Sayan
        @"^\s*(?:show|list)\s+(?:me\s+)?users?\s+(?:named|called)\s+(?<name>.+?)\s*[?.!]*\s*$"
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

            // Remove common conversational suffixes.
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
