
namespace SemanticKernelPlatform.Application.Models;

public sealed class TicketAnalysisSummary
{
    public int TotalTickets { get; set; }

    public List<TicketStatusMetric> StatusMetrics { get; set; } = [];

    public List<string> HighestVolumeStatuses { get; set; } = [];

    public List<string> LowestVolumeStatuses { get; set; } = [];
}
