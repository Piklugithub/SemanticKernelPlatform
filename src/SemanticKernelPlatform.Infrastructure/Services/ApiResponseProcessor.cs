
using System.Text.Json;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Infrastructure.Services;

public sealed class ApiResponseProcessor
{
    public ChatTable? CreateTable(
        string jsonResponse,
        string title)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
            return null;

        try
        {
            using var document = JsonDocument.Parse(jsonResponse);

            var root = document.RootElement;

            // Extract actual data from API response envelopes.
            if (TryGetPropertyIgnoreCase(root, "data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Null)
                    return CreateEmptyTable(title);

                // Some APIs return JSON inside a string.
                if (data.ValueKind == JsonValueKind.String)
                {
                    var dataString = data.GetString();

                    if (string.IsNullOrWhiteSpace(dataString))
                        return CreateEmptyTable(title);

                    using var dataDocument = JsonDocument.Parse(dataString);

                    return CreateTableFromElement(
                        dataDocument.RootElement,
                        title);
                }

                return CreateTableFromElement(data, title);
            }

            return CreateTableFromElement(root, title);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChatTable? CreateTableFromElement(
        JsonElement element,
        string title)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array =>
                CreateTableFromArray(element, title),

            JsonValueKind.Object =>
                CreateTableFromObject(element, title),

            JsonValueKind.String or
            JsonValueKind.Number or
            JsonValueKind.True or
            JsonValueKind.False =>
                CreateTableFromPrimitive(element, title),

            JsonValueKind.Null =>
                CreateEmptyTable(title),

            _ => null
        };
    }

    private static ChatTable CreateTableFromArray(
        JsonElement root,
        string title)
    {
        var records = root.EnumerateArray().ToList();

        if (records.Count == 0)
            return CreateEmptyTable(title);

        if (records.All(x => x.ValueKind == JsonValueKind.Object))
        {
            return CreateTableFromObjectArray(records, title);
        }

        return new ChatTable
        {
            Title = title,
            Columns = ["Value"],
            Rows = records
                .Select(x => new List<string?>
                {
                    GetDisplayValue(x)
                })
                .ToList(),
            TotalRecords = records.Count
        };
    }

    private static ChatTable CreateTableFromObjectArray(
        List<JsonElement> records,
        string title)
    {
        var columns = records
            .SelectMany(record => record.EnumerateObject())
            .Where(property => !IsExcludedProperty(property.Name))
            .Select(property => property.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<List<string?>>();

        foreach (var record in records)
        {
            var properties = record
                .EnumerateObject()
                .Where(property => !IsExcludedProperty(property.Name))
                .ToDictionary(
                    property => property.Name,
                    property => property.Value,
                    StringComparer.OrdinalIgnoreCase);

            var row = new List<string?>();

            foreach (var column in columns)
            {
                row.Add(
                    properties.TryGetValue(column, out var value)
                        ? GetDisplayValue(value)
                        : null);
            }

            rows.Add(row);
        }

        return new ChatTable
        {
            Title = title,
            Columns = columns,
            Rows = rows,
            TotalRecords = rows.Count
        };
    }

    private static ChatTable CreateTableFromObject(
        JsonElement root,
        string title)
    {
        var properties = root
            .EnumerateObject()
            .Where(property => !IsExcludedProperty(property.Name))
            .ToList();

        // Scalar object properties represent status/count pairs.
        var isScalarObject = properties.All(property =>
            property.Value.ValueKind is
                JsonValueKind.String or
                JsonValueKind.Number or
                JsonValueKind.True or
                JsonValueKind.False or
                JsonValueKind.Null);

        if (isScalarObject)
        {
            return new ChatTable
            {
                Title = title,
                Columns = ["Status", "Count"],
                Rows = properties
                    .Select(property => new List<string?>
                    {
                        property.Name,
                        GetDisplayValue(property.Value)
                    })
                    .ToList(),
                TotalRecords = properties.Count
            };
        }

        return new ChatTable
        {
            Title = title,
            Columns = ["Property", "Value"],
            Rows = properties
                .Select(property => new List<string?>
                {
                    property.Name,
                    GetDisplayValue(property.Value)
                })
                .ToList(),
            TotalRecords = properties.Count
        };
    }

    private static ChatTable CreateTableFromPrimitive(
        JsonElement root,
        string title)
    {
        return new ChatTable
        {
            Title = title,
            Columns = ["Value"],
            Rows =
            [
                new List<string?>
                {
                    GetDisplayValue(root)
                }
            ],
            TotalRecords = 1
        };
    }

    private static ChatTable CreateEmptyTable(string title)
    {
        return new ChatTable
        {
            Title = title,
            Columns = [],
            Rows = [],
            TotalRecords = 0
        };
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool IsExcludedProperty(string propertyName)
    {
        return propertyName.Contains(
            "Base64",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDisplayValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            JsonValueKind.Object or JsonValueKind.Array =>
                value.GetRawText(),
            _ => null
        };
    }
}
