
using SemanticKernelPlatform.Application.Interfaces;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class ApiResponseContext : IApiResponseContext
{
    public string? OperationName { get; private set; }

    public string? RawResponse { get; private set; }

    public string? Reply { get; private set; }

    public string ResponseType { get; private set; } = "Text";

    public void Clear()
    {
        OperationName = null;
        RawResponse = null;
        Reply = null;
        ResponseType = "Text";
    }

    public void SetResponse(
        string operationName,
        string rawResponse)
    {
        OperationName = operationName;
        RawResponse = rawResponse;
        Reply = null;
        ResponseType = "Table";
    }

    public void SetTextResponse(
        string operationName,
        string reply,
        string responseType = "Text")
    {
        OperationName = operationName;
        RawResponse = null;
        Reply = reply;
        ResponseType = responseType;
    }
}
