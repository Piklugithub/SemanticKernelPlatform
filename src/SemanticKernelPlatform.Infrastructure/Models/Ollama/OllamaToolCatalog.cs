
namespace SemanticKernelPlatform.Infrastructure.Models.Ollama;

public static class OllamaToolCatalog
{
    public static IReadOnlyList<OllamaToolDefinition> GetTools()
    {
        return
        [
            new OllamaToolDefinition
            {
                Type = "function",
                Function = new OllamaFunctionDefinition
                {
                    Name = "get_users",
                    Description =
                        "Retrieves all registered users from the Ticket Management system. " +
                        "Use this tool when the user asks to list, show or retrieve users.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new { }
                    }
                }
            },

            new OllamaToolDefinition
            {
                Type = "function",
                Function = new OllamaFunctionDefinition
                {
                    Name = "get_groups",
                    Description =
                        "Retrieves all groups configured in the Ticket Management system. " +
                        "Use this tool when the user asks about groups or teams.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new { }
                    }
                }
            },

            new OllamaToolDefinition
            {
                Type = "function",
                Function = new OllamaFunctionDefinition
                {
                    Name = "get_ticket_status_counts",
                    Description =
                        "Retrieves the actual number of tickets grouped by status, " +
                        "such as Open, Active, Resolved and On Hold. " +
                        "Use this tool for ticket counts, ticket statistics " +
                        "or ticket distribution.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new { }
                    }
                }
            }
        ];
    }
}
