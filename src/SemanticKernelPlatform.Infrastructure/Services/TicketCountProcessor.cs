
using System.Globalization;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class TicketCountProcessor
{
    private static readonly string[] StatusColumnNames =
    [
        "status",
        "ticketStatus",
        "state"
    ];

    private static readonly string[] CountColumnNames =
    [
        "count",
        "total",
        "ticketCount",
        "totalCount",
        "value"
    ];


    public ChatResponse GetCountByStatus(
        ChatTable ticketTable,
        string requestedStatus)
    {
        ArgumentNullException.ThrowIfNull(ticketTable);

        if (string.IsNullOrWhiteSpace(requestedStatus))
        {
            return CreateTextResponse(
                "Please specify a ticket status.");
        }

        var statusColumnIndex = FindColumnIndex(
            ticketTable.Columns,
            StatusColumnNames);

        var countColumnIndex = FindColumnIndex(
            ticketTable.Columns,
            CountColumnNames);

        if (statusColumnIndex < 0 || countColumnIndex < 0)
        {
            return CreateTextResponse(
                "Unable to identify the status and count columns.");
        }

        var normalizedRequestedStatus = NormalizeStatus(requestedStatus);

        var matchingRows = ticketTable.Rows
            .Where(row =>
                row.Count > Math.Max(statusColumnIndex, countColumnIndex) &&
                NormalizeStatus(row[statusColumnIndex] ?? string.Empty)
                    == normalizedRequestedStatus)
            .ToList();

        if (matchingRows.Count == 0)
        {
            return CreateTextResponse(
                $"No ticket status matching '{requestedStatus}' was found.");
        }

        if (matchingRows.Count > 1)
        {
            return CreateTextResponse(
                $"Multiple entries were found for '{requestedStatus}'.");
        }

        var countValue = matchingRows[0][countColumnIndex];

        if (!decimal.TryParse(
                countValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var count))
        {
            return CreateTextResponse(
                $"The ticket count for '{requestedStatus}' is invalid.");
        }

        return CreateTextResponse(
            $"There are {count.ToString("0.################", CultureInfo.InvariantCulture)} tickets with status '{requestedStatus}'.");
    }

    private static string NormalizeStatus(string status)
    {
        return new string(
            status
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }


    private static int FindColumnIndex(
        List<string> columns,
        IEnumerable<string> possibleNames)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (possibleNames.Any(name =>
                string.Equals(
                    columns[i],
                    name,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        return -1;
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
