
namespace SemanticKernelPlatform.Application.Models;

public sealed class UserIntent
{
    public IntentType Intent { get; set; } = IntentType.Unknown;

    public string? SearchTerm { get; set; }

    public string? Status { get; set; }
}

public enum IntentType
{
    Unknown = 0,

    // User-related operations
    GetAllUsers = 1,
    SearchUsers = 2,

    // Ticket-related operations
    GetTicketCount = 3,
    GetTicketCountsByStatus = 4,

    // Group-related operations
    GetGroups = 5
}
