
using System.Globalization;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class TicketAnalysisProcessor
{
    private static readonly string[] StatusColumnAliases =
    [
        "status",
        "ticketstatus",
        "state"
    ];

    private static readonly string[] CountColumnAliases =
    [
        "count",
        "total",
        "ticketcount",
        "totalcount",
        "value"
    ];

    public TicketAnalysisSummary Analyze(ChatTable ticketTable)
    {
        ArgumentNullException.ThrowIfNull(ticketTable);

        var statusIndex = FindColumnIndex(
            ticketTable.Columns,
            StatusColumnAliases);

        var countIndex = FindColumnIndex(
            ticketTable.Columns,
            CountColumnAliases);

        if (statusIndex < 0 || countIndex < 0)
        {
            throw new InvalidOperationException(
                "Ticket table must contain status and count columns.");
        }

        var statusCounts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in ticketTable.Rows)
        {
            if (row.Count <= Math.Max(statusIndex, countIndex))
                continue;

            var status = row[statusIndex]?.Trim();
            var countValue = row[countIndex];

            if (string.IsNullOrWhiteSpace(status))
                continue;

            if (!int.TryParse(
                    countValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var count))
            {
                throw new InvalidOperationException(
                    $"Invalid ticket count for status '{status}'.");
            }

            if (count < 0)
            {
                throw new InvalidOperationException(
                    $"Ticket count cannot be negative for status '{status}'.");
            }

            if (statusCounts.ContainsKey(status))
            {
                statusCounts[status] += count;
            }
            else
            {
                statusCounts[status] = count;
            }
        }

        var totalTickets = statusCounts.Values.Sum();

        if (statusCounts.Count == 0 || totalTickets == 0)
        {
            throw new InvalidOperationException(
                "No valid ticket status data is available for analysis.");
        }

        var metrics = statusCounts
            .Select(item => new TicketStatusMetric
            {
                Status = item.Key,
                Count = item.Value,
                Percentage = Math.Round(
                    item.Value * 100m / totalTickets,
                    2)
            })
            .OrderByDescending(metric => metric.Count)
            .ToList();

        var highestCount = metrics.Max(metric => metric.Count);
        var lowestCount = metrics.Min(metric => metric.Count);

        return new TicketAnalysisSummary
        {
            TotalTickets = totalTickets,

            StatusMetrics = metrics,

            HighestVolumeStatuses = metrics
                .Where(metric => metric.Count == highestCount)
                .Select(metric => metric.Status)
                .ToList(),

            LowestVolumeStatuses = metrics
                .Where(metric => metric.Count == lowestCount)
                .Select(metric => metric.Status)
                .ToList()
        };
    }

    private static int FindColumnIndex(
        List<string> columns,
        string[] aliases)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var normalizedColumn = Normalize(columns[i]);

            if (aliases.Any(alias =>
                    Normalize(alias) == normalizedColumn))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Normalize(string value)
    {
        return new string(
            value.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }
}
