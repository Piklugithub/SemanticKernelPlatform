
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class IntentExtractionService : IIntentExtractionService
{
    private readonly IChatCompletionService _chatCompletion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IntentExtractionService(Kernel kernel)
    {
        _chatCompletion = kernel.Services
            .GetRequiredService<IChatCompletionService>();
    }

    public async Task<UserIntent> ExtractIntentAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new UserIntent();
        }

        var history = new ChatHistory();

        history.AddSystemMessage("""
            You are an intent extraction engine for an enterprise application.

            Your responsibility is to understand the user's request and
            extract its intent and relevant parameters.

            You must return ONLY valid JSON.
            Do not include Markdown, explanations or code fences.

            Supported intents:

            1. GetAllUsers
               The user wants to retrieve all registered users.

            2. SearchUsers
               The user wants to find a user or retrieve a user's details.
               Extract the person's name into searchTerm.

            3. GetTicketCount
               The user wants the count for one specific ticket status.
               Extract the status into status.

            4. GetTicketCountsByStatus
               The user wants ticket counts grouped by status.

            5. GetGroups
               The user wants to retrieve all groups or teams.

            6. Unknown
               Use this when the request does not match a supported intent
               or there is insufficient information.

            Supported ticket statuses:
            - Open
            - Active
            - Resolved
            - On Hold

            Important rules:

            - Never invent a person's name.
            - Never invent a ticket status.
            - Do not calculate ticket counts.
            - Do not return actual business data.
            - If the user asks for one status count, use GetTicketCount.
            - If the user asks for all status counts, use GetTicketCountsByStatus.
            - If the user asks about a specific person, use SearchUsers.
            - If the user asks for all users, use GetAllUsers.
            - If the request is ambiguous, use Unknown.

            Return exactly this JSON structure:

            {
                "intent": "Unknown",
                "searchTerm": null,
                "status": null
            }
            """);

        history.AddUserMessage(userMessage);

        var response = await _chatCompletion.GetChatMessageContentAsync(
            history,
            cancellationToken: cancellationToken);

        var responseContent = response.Content;

        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return new UserIntent();
        }

        try
        {
            var json = ExtractJson(responseContent);

            var extractedIntent =
                JsonSerializer.Deserialize<IntentExtractionResult>(
                    json,
                    JsonOptions);

            if (extractedIntent is null)
            {
                return new UserIntent();
            }

            return MapToUserIntent(extractedIntent);
        }
        catch (JsonException)
        {
            // Invalid model output must never trigger business operations.
            return new UserIntent();
        }
    }

    private static UserIntent MapToUserIntent(
        IntentExtractionResult result)
    {
        var intent = result.Intent?.Trim().ToLowerInvariant();

        var intentType = intent switch
        {
            "getallusers" => IntentType.GetAllUsers,
            "searchusers" => IntentType.SearchUsers,
            "getticketcount" => IntentType.GetTicketCount,
            "getticketcountsbystatus" => IntentType.GetTicketCountsByStatus,
            "getgroups" => IntentType.GetGroups,
            _ => IntentType.Unknown
        };

        if (intentType == IntentType.SearchUsers &&
            string.IsNullOrWhiteSpace(result.SearchTerm))
        {
            intentType = IntentType.Unknown;
        }

        if (intentType == IntentType.GetTicketCount)
        {
            var normalizedStatus = NormalizeTicketStatus(result.Status);

            if (normalizedStatus is null)
            {
                intentType = IntentType.Unknown;
            }
            else
            {
                result.Status = normalizedStatus;
            }
        }

        return new UserIntent
        {
            Intent = intentType,
            SearchTerm = result.SearchTerm?.Trim(),
            Status = result.Status
        };
    }

    private static string? NormalizeTicketStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "open" => "Open",
            "active" => "Active",
            "resolved" => "Resolved",
            "on hold" or "onhold" or "on-hold" => "On Hold",
            _ => null
        };
    }

    private static string ExtractJson(string content)
    {
        var json = content.Trim();

        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = json.IndexOf('\n');

            if (firstNewLine >= 0)
            {
                json = json[(firstNewLine + 1)..];
            }

            var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);

            if (closingFence >= 0)
            {
                json = json[..closingFence];
            }
        }

        var startIndex = json.IndexOf('{');
        var endIndex = json.LastIndexOf('}');

        if (startIndex < 0 || endIndex <= startIndex)
        {
            throw new JsonException(
                "The model did not return a valid JSON object.");
        }

        return json[startIndex..(endIndex + 1)];
    }

    private sealed class IntentExtractionResult
    {
        public string? Intent { get; set; }

        public string? SearchTerm { get; set; }

        public string? Status { get; set; }
    }
}
