
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using SemanticKernelPlatform.Application.Interfaces;

namespace SemanticKernelPlatform.Plugins.Http;

public sealed class HttpApiPlugin
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IApiResponseContext _apiResponseContext;

    public HttpApiPlugin(
        HttpClient httpClient,
        IConfiguration configuration,
        IApiResponseContext apiResponseContext)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _apiResponseContext = apiResponseContext;
    }

    [KernelFunction("GetTicketStatusCounts")]
    [Description(
        "Retrieves the actual number of tickets grouped by status, " +
        "such as Open, Active, Resolved and On Hold. " +
        "Use this function whenever the user asks about ticket counts, " +
        "ticket statistics, ticket distribution or an overall ticket summary.")]
    public Task<string> GetTicketStatusCountsAsync(
        CancellationToken cancellationToken = default)
    {
        return CallConfiguredGetApiAsync(
            "TicketManagement",
            "GetTicketStatusCounts",
            cancellationToken);
    }

    [KernelFunction("GetUsers")]
    [Description(
        "Retrieves the actual registered users from the Ticket Management system. " +
        "Use this function when the user asks to list, show, find or retrieve users. " +
        "Return the API response data without modifying user names, IDs or other fields.")]
    public Task<string> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        return CallConfiguredGetApiAsync(
            "TicketManagement",
            "GetUsers",
            cancellationToken);
    }

    [KernelFunction("GetGroups")]
    [Description(
        "Retrieves all groups configured in the Ticket Management system. " +
        "Use this function when the user asks about groups or teams.")]
    public Task<string> GetGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        return CallConfiguredGetApiAsync(
            "TicketManagement",
            "GetGroups",
            cancellationToken);
    }

    private async Task<string> CallConfiguredGetApiAsync(
        string integrationName,
        string operationName,
        CancellationToken cancellationToken)
    {
        var integration = _configuration
            .GetSection($"ApiIntegrations:{integrationName}");

        if (!integration.Exists())
        {
            return $"Integration '{integrationName}' is not configured.";
        }

        var baseUrl = integration["BaseUrl"];

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp &&
             baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return $"Integration '{integrationName}' has an invalid BaseUrl.";
        }

        var operation = integration
            .GetSection($"Operations:{operationName}");

        if (!operation.Exists())
        {
            return $"Operation '{operationName}' is not configured.";
        }

        var path = operation["Path"];

        if (string.IsNullOrWhiteSpace(path) ||
            Uri.TryCreate(path, UriKind.Absolute, out _) ||
            path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\'))
        {
            return "The configured operation path is invalid.";
        }

        var normalizedBaseUrl = baseUri.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
                ? baseUri.AbsoluteUri
                : baseUri.AbsoluteUri + "/";

        var requestUri = new Uri(new Uri(normalizedBaseUrl), path);

        // Ensure the configured path cannot redirect the request
        // outside the approved integration host.
        if (requestUri.Scheme != baseUri.Scheme ||
            requestUri.Host != baseUri.Host ||
            requestUri.Port != baseUri.Port)
        {
            return "The configured operation URL is outside the approved host.";
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                requestUri,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return $"""
                    API_CALL_FAILED
                    Integration: {integrationName}
                    Operation: {operationName}
                    HTTP Status: {(int)response.StatusCode}
                    """;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;

            // Validate and capture the original JSON response.
            if (contentType?.Contains(
                    "json",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    using var document = JsonDocument.Parse(responseBody);

                    // Preserve the original API response.
                    // Do not serialize or modify the JSON.
                    _apiResponseContext.SetResponse(
                        operationName,
                        responseBody);
                }
                catch (JsonException)
                {
                    return """
                        API_RESPONSE_INVALID_JSON
                        The configured API returned content that could not be parsed as JSON.
                        """;
                }
            }

            // Return the original API response to Semantic Kernel.
            return $"""
                API_CALL_SUCCESS
                Integration: {integrationName}
                Operation: {operationName}
                HTTP Status: {(int)response.StatusCode}

                The API request completed successfully.
                The original API response has been captured by the backend.
                Do not reproduce or invent API records.
                """;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return "Unable to connect to the configured API.";
        }
        catch (OperationCanceledException)
        {
            return "The API request timed out.";
        }
    }
}
