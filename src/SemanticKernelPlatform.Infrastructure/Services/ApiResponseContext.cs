using SemanticKernelPlatform.Application.Interfaces;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class ApiResponseContext : IApiResponseContext
{
    public string? RawResponse { get; private set; }

    public string? OperationName { get; private set; }

    public void SetResponse(string operationName, string rawResponse)
    {
        OperationName = operationName;
        RawResponse = rawResponse;
    }

    public void Clear()
    {
        OperationName = null;
        RawResponse = null;
    }
}