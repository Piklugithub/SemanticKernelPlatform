
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
            },

            new OllamaToolDefinition
            {
                Type = "function",
                Function = new OllamaFunctionDefinition
                {
                    Name = "search_users",
                    Description =
                        "Searches registered users by their name. " +
                        "Use this tool when the user wants to find users " +
                        "matching a particular name or search term.",

                    Parameters = new
                    {
                        type = "object",

                        properties = new
                        {
                            searchTerm = new
                            {
                                type = "string",
                                description =
                                    "The name or partial name to search for."
                            }
                        },

                        required = new[]
                        {
                            "searchTerm"
                        }
                    }
                }
            },

            new OllamaToolDefinition
            {
                Type = "function",
                Function = new OllamaFunctionDefinition
                {
                    Name = "get_ticket_count",
                    Description =
                        "Retrieves the number of tickets for a specific status. " +
                        "Use this tool when the user asks how many tickets are Open, " +
                        "Active, Resolved or On Hold.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            status = new
                            {
                                type = "string",
                                description =
                                    "The ticket status. Supported values: " +
                                    "Open, Active, Resolved, On Hold."
                            }
                        },
                        required = new[] { "status" }
                    }
                }
            }

        ];
    }
}
