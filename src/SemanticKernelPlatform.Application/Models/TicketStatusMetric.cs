
namespace SemanticKernelPlatform.Application.Models;

public sealed class TicketStatusMetric
{
    public string Status { get; set; } = string.Empty;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}
