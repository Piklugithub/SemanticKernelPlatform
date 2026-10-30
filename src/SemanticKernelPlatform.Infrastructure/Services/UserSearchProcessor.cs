
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class UserSearchProcessor
{
    public ChatResponse SearchUsers(
        ChatTable usersTable,
        string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(usersTable);

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return CreateTextResponse(
                "Please provide a name to search.");
        }

        // Find the FullName column dynamically.
        var nameColumnIndex = usersTable.Columns.FindIndex(
            column => string.Equals(
                column,
                "fullName",
                StringComparison.OrdinalIgnoreCase));

        if (nameColumnIndex < 0)
        {
            return CreateTextResponse(
                "Unable to search users because the FullName column was not found.");
        }

        var normalizedSearchTerm = searchTerm.Trim();

        // First, find all exact matches.
        var matchingRows = usersTable.Rows
            .Where(row =>
                row.Count > nameColumnIndex &&
                string.Equals(
                    row[nameColumnIndex]?.Trim(),
                    normalizedSearchTerm,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var matchType = "exact";

        // If no exact match exists, find all partial matches.
        if (matchingRows.Count == 0)
        {
            matchingRows = usersTable.Rows
                .Where(row =>
                    row.Count > nameColumnIndex &&
                    row[nameColumnIndex]?.Contains(
                        normalizedSearchTerm,
                        StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            matchType = "partial";
        }

        if (matchingRows.Count == 0)
        {
            return CreateTextResponse(
                $"No registered users found matching '{normalizedSearchTerm}'.");
        }

        var resultTable = new ChatTable
        {
            Title = "Matching Registered Users",
            Columns = usersTable.Columns,
            Rows = matchingRows,
            TotalRecords = matchingRows.Count
        };

        var reply = matchingRows.Count == 1
            ? $"Yes, a registered user matching '{normalizedSearchTerm}' was found."
            : $"Found {matchingRows.Count} registered users matching '{normalizedSearchTerm}'.";

        return new ChatResponse
        {
            Reply = reply,
            ResponseType = "Table",
            Table = resultTable
        };
    }

    private static ChatResponse CreateTextResponse(string message)
    {
        return new ChatResponse
        {
            Reply = message,
            ResponseType = "Text",
            Table = null
        };
    }
}
