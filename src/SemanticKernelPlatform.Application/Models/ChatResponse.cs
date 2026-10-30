namespace SemanticKernelPlatform.Application.Models;

public sealed class ChatResponse
{
    public string Reply { get; set; } = string.Empty;

    public string ResponseType { get; set; } = "Text";

    public ChatTable? Table { get; set; }
}

public sealed class ChatTable
{
    public string Title { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = [];

    public List<List<string?>> Rows { get; set; } = [];

    public int TotalRecords { get; set; }
}