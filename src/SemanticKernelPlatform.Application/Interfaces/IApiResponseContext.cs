namespace SemanticKernelPlatform.Application.Interfaces;

public interface IApiResponseContext
{
    string? RawResponse { get; }

    string? OperationName { get; }

    void SetResponse(string operationName, string rawResponse);

    void Clear();
}