using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterLan.Contracts.Crm;

namespace InterLan.Application.Crm;

public sealed class CrmImportParser
{
    public const string SupportedSchema = "interlan.work-package.v1";
    public const int MaxPayloadBytes = 512 * 1024;

    private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "command",
        "shell",
        "script",
        "exec",
        "executable",
        "process",
        "arguments",
        "password",
        "token",
        "secret",
        "credential",
        "privatekey",
        "environment",
        "env"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public CrmWorkPackageImportV1 Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            throw new InvalidDataException($"CRM import payload exceeds {MaxPayloadBytes} bytes.");

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        });

        RequireDataOnly(document.RootElement, "$", depth: 0);

        CrmWorkPackageImportV1 payload;
        try
        {
            payload = JsonSerializer.Deserialize<CrmWorkPackageImportV1>(json, SerializerOptions)
                ?? throw new InvalidDataException("CRM import payload was empty after parsing.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("CRM import payload does not match the supported declarative schema.", exception);
        }

        if (!string.Equals(payload.Schema, SupportedSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported CRM import schema '{payload.Schema}'. Expected '{SupportedSchema}'.");

        return payload;
    }

    private static void RequireDataOnly(JsonElement element, string path, int depth)
    {
        if (depth > 32)
            throw new InvalidDataException("CRM import payload exceeds maximum nesting depth.");

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal)
                        .Replace("-", string.Empty, StringComparison.Ordinal);
                    if (ForbiddenPropertyNames.Contains(normalized))
                        throw new InvalidDataException($"CRM import contains prohibited executable or secret-bearing field '{path}.{property.Name}'.");
                    RequireDataOnly(property.Value, $"{path}.{property.Name}", depth + 1);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    RequireDataOnly(item, $"{path}[{index}]", depth + 1);
                    index++;
                }
                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                break;
            default:
                throw new InvalidDataException($"CRM import contains unsupported JSON value at '{path}'.");
        }
    }
}
