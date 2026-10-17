using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TaskManager.Api.Tests.Support;

/// <summary>
/// コミット済みの OpenAPI 文書（docs/api/openapi.v1.json）を API の契約として扱う。
/// 応答本文がスキーマに合うかを検証する最小限の検証器（OD-26: 依存ライブラリを増やさず自作。
/// type / nullable 型 / required / properties / items / enum / oneOf / $ref / format(uuid, date, date-time) /
/// minLength / maxLength に対応。この文書で使われている構文に限る）。
/// </summary>
public static partial class OpenApiContract
{
    private static readonly Lazy<JsonObject> Snapshot = new(() =>
        JsonNode.Parse(File.ReadAllText(SnapshotPath))!.AsObject());

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string SnapshotPath => Path.Combine(RepositoryRoot, "docs", "api", "openapi.v1.json");

    public static JsonObject Document => Snapshot.Value;

    /// <summary>(operationId, method, path, operation)</summary>
    public static IEnumerable<(string OperationId, string Method, string Path, JsonObject Operation)> Operations()
    {
        foreach (var (path, item) in Document["paths"]!.AsObject())
        {
            foreach (var (method, op) in item!.AsObject())
            {
                if (op is JsonObject operation && operation["operationId"] is { } id)
                {
                    yield return (id.GetValue<string>(), method.ToUpperInvariant(), path, operation);
                }
            }
        }
    }

    public static JsonObject Operation(string operationId) =>
        Operations().SingleOrDefault(o => o.OperationId == operationId).Operation
        ?? throw new InvalidOperationException($"operationId {operationId} が OpenAPI 文書にありません。");

    /// <summary>応答のステータスが文書に定義され、本文がスキーマに合うことを確認する。</summary>
    public static async Task AssertResponseAsync(HttpResponseMessage response, string operationId)
    {
        ArgumentNullException.ThrowIfNull(response);
        var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var responses = Operation(operationId)["responses"]!.AsObject();
        Assert.True(responses.ContainsKey(status),
            $"{operationId} の応答 {status} は OpenAPI 文書に定義されていません（定義: {string.Join(", ", responses.Select(r => r.Key))}）。");
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var content = responses[status]!["content"] as JsonObject;
        if (content is null || mediaType is null || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return;
        }

        var schema = content[mediaType]?["schema"];
        if (schema is null)
        {
            // バイナリ（ダウンロード）など、スキーマのない応答
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var errors = new List<string>();
        Validate(JsonNode.Parse(body), schema.AsObject(), "$", errors);
        Assert.True(errors.Count == 0, $"{operationId} {status} の応答が OpenAPI のスキーマと一致しません:\n{string.Join('\n', errors)}\n本文: {body}");
    }

    /// <summary>コンポーネント名のスキーマで検証する（単体の確認用）。</summary>
    public static IReadOnlyList<string> ValidateAgainst(string componentName, JsonNode? value)
    {
        var errors = new List<string>();
        Validate(value, ResolveRef($"#/components/schemas/{componentName}"), "$", errors);
        return errors;
    }

    private static void Validate(JsonNode? value, JsonObject schema, string path, List<string> errors)
    {
        if (schema["$ref"] is { } reference)
        {
            Validate(value, ResolveRef(reference.GetValue<string>()), path, errors);
            return;
        }

        if (schema["oneOf"] is JsonArray oneOf)
        {
            var matches = oneOf.Count(option =>
            {
                var optionErrors = new List<string>();
                Validate(value, option!.AsObject(), path, optionErrors);
                return optionErrors.Count == 0;
            });
            if (matches != 1)
            {
                errors.Add($"{path}: oneOf のうち一致したものが {matches} 件");
            }

            return;
        }

        var types = schema["type"] switch
        {
            JsonValue v => [v.GetValue<string>()],
            JsonArray a => a.Select(t => t!.GetValue<string>()).ToArray(),
            _ => Array.Empty<string>(),
        };
        var actual = KindOf(value);
        if (types.Length > 0 && !types.Any(t => t == actual || (t == "number" && actual == "integer")))
        {
            errors.Add($"{path}: 型 {actual} は {string.Join("|", types)} ではない");
            return;
        }

        if (schema["enum"] is JsonArray enumValues && value is not null
            && !enumValues.Any(e => JsonNode.DeepEquals(e, value)))
        {
            errors.Add($"{path}: {value.ToJsonString()} は列挙値 [{string.Join(", ", enumValues.Select(e => e!.ToJsonString()))}] にない");
        }

        switch (value)
        {
            case JsonObject obj:
                foreach (var required in schema["required"]?.AsArray() ?? [])
                {
                    var name = required!.GetValue<string>();
                    if (!obj.ContainsKey(name))
                    {
                        errors.Add($"{path}: 必須プロパティ {name} がない");
                    }
                }

                var properties = schema["properties"] as JsonObject;
                var additional = schema["additionalProperties"];
                foreach (var (name, propertyValue) in obj)
                {
                    if (properties?[name] is JsonObject propertySchema)
                    {
                        Validate(propertyValue, propertySchema, $"{path}.{name}", errors);
                    }
                    else if (additional is JsonObject additionalSchema)
                    {
                        Validate(propertyValue, additionalSchema, $"{path}.{name}", errors);
                    }
                    else if (properties is not null && !(additional is JsonValue allow && allow.GetValue<bool>()))
                    {
                        // 文書にないプロパティを返していないか（契約にない情報の漏れも検出する）
                        errors.Add($"{path}: 文書にないプロパティ {name}");
                    }
                }

                break;
            case JsonArray array when schema["items"] is JsonObject items:
                for (var i = 0; i < array.Count; i++)
                {
                    Validate(array[i], items, $"{path}[{i}]", errors);
                }

                break;
            case JsonValue scalar when actual == "string":
                ValidateString(scalar.GetValue<string>(), schema, path, errors);
                break;
        }
    }

    private static void ValidateString(string text, JsonObject schema, string path, List<string> errors)
    {
        var format = schema["format"]?.GetValue<string>();
        var ok = format switch
        {
            "uuid" => Guid.TryParseExact(text, "D", out _),
            "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "date-time" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) && DateTimeRegex().IsMatch(text),
            _ => true,
        };
        if (!ok)
        {
            errors.Add($"{path}: \"{text}\" は format {format} ではない");
        }

        if (schema["minLength"] is { } min && text.Length < min.GetValue<int>())
        {
            errors.Add($"{path}: 文字数 {text.Length} が minLength 未満");
        }

        if (schema["maxLength"] is { } max && text.Length > max.GetValue<int>())
        {
            errors.Add($"{path}: 文字数 {text.Length} が maxLength 超過");
        }
    }

    private static string KindOf(JsonNode? value) => value switch
    {
        null => "null",
        JsonObject => "object",
        JsonArray => "array",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => v.ToJsonString().Contains('.', StringComparison.Ordinal) ? "number" : "integer",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null => "null",
            _ => "unknown",
        },
        _ => "unknown",
    };

    private static JsonObject ResolveRef(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"外部参照には対応していません: {reference}");
        }

        JsonNode? node = Document;
        foreach (var segment in reference[2..].Split('/'))
        {
            node = node?[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)];
        }

        return node as JsonObject ?? throw new InvalidOperationException($"参照先がありません: {reference}");
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "api", "openapi.v1.json")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("リポジトリのルート（docs/api/openapi.v1.json）が見つかりません。");
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimeRegex();
}
