namespace SemanticKernelPlatform.Application.Interfaces;

public interface IApiResponseContext
{
    string? OperationName { get; }
    string? RawResponse { get; }
    string? Reply { get; }
    string ResponseType { get; }

    void Clear();

    void SetResponse(
        string operationName,
        string rawResponse);

    void SetTextResponse(
        string operationName,
        string reply,
        string responseType = "Text");
}