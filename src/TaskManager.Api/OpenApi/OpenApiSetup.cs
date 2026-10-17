using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using TaskManager.Api.Security;

namespace TaskManager.Api.OpenApi;

/// <summary>
/// OpenAPI 文書（/openapi/v1.json）の設定（docs/design/api-design.md §2, §6）。
/// 共通のエラー応答・セキュリティ要件・必要な権限を全操作に付ける。Secret・内部情報は含めない。
/// </summary>
public static class OpenApiSetup
{
    public const string DocumentName = "v1";
    public const string BearerSchemeName = "bearerAuth";
    public const string RefreshCookieSchemeName = "refreshCookie";
    private const string ProblemJson = "application/problem+json";

    private static readonly (string Name, string Description)[] TagDescriptions =
    [
        ("Auth", "ログイン・ログアウト・トークン更新・パスワード変更"),
        ("Me", "ログイン中の利用者自身の情報"),
        ("Users", "利用者管理（システム管理者のみ）"),
        ("UserDirectory", "メンバー追加用の利用者検索"),
        ("Projects", "プロジェクト"),
        ("ProjectMembers", "プロジェクトのメンバーとロール"),
        ("WorkflowStatuses", "プロジェクトのステータス定義"),
        ("Tags", "プロジェクトのタグ定義"),
        ("Milestones", "マイルストーン"),
        ("Tasks", "タスク"),
        ("TaskComments", "タスクのコメント"),
        ("TaskAttachments", "タスクの添付ファイル"),
        ("TaskHistory", "タスクの変更履歴"),
        ("MyTasks", "自分のタスク"),
        ("Notices", "お知らせ"),
        ("Search", "検索"),
        ("System", "システム情報（環境確認用）"),
    ];

    public static IServiceCollection AddTaskManagerOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "TaskManager API",
                    Version = DocumentName,
                    Description = "チームのタスクを管理するための REST API。エラーは RFC 9457 の Problem Details（code・traceId・errors を拡張）で返す。",
                };
                document.Servers = [new OpenApiServer { Url = "/" }];
                document.Tags = new HashSet<OpenApiTag>(TagDescriptions.Select(t => new OpenApiTag { Name = t.Name, Description = t.Description }));

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "/api/v1/auth/login で取得したアクセストークン（有効期限 15 分）。",
                };
                document.Components.SecuritySchemes[RefreshCookieSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Cookie,
                    Name = "tm_refresh",
                    Description = "HttpOnly のリフレッシュトークン Cookie。/api/v1/auth/refresh と /api/v1/auth/logout のみで使用。",
                };
                return Task.CompletedTask;
            });

            // Problem Details の拡張項目（api-design.md §1.4, §2.5）
            options.AddSchemaTransformer((schema, context, _) =>
            {
                // 列挙は文字列。null 許容は参照側（oneOf: null）で表すため、列挙の定義自体から null を除く
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
                if (type.IsEnum && schema.Enum is { Count: > 0 } values)
                {
                    schema.Enum = values.Where(v => v is not null).ToList();
                    schema.Type = JsonSchemaType.String;
                }

                if (context.JsonTypeInfo.Type == typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))
                {
                    schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                    schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "機械判定用のエラーコード（例: validation.failed）" };
                    schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "問い合わせ用の識別子" };
                    schema.Properties["errors"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object | JsonSchemaType.Null,
                        Description = "入力項目ごとのエラー（キーは Request の JSON プロパティ名）",
                        AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
                    };
                    schema.Required ??= new HashSet<string>();
                    schema.Required.Add("code");
                    schema.Required.Add("traceId");
                }

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                // クエリパラメータ名を camelCase にする（api-design.md §1。サーバー側の受け取りは大文字小文字を区別しない）
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parameter.In == ParameterLocation.Query && parameter.Name is { Length: > 0 } name && char.IsUpper(name[0]))
                    {
                        parameter.Name = char.ToLowerInvariant(name[0]) + name[1..];
                    }
                }

                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
                var usesRefreshCookie = metadata.OfType<RequireSameOriginAttribute>().Any();
                operation.Security ??= [];
                if (usesRefreshCookie)
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(RefreshCookieSchemeName, context.Document)] = [],
                    });
                }
                else if (!allowsAnonymous)
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerSchemeName, context.Document)] = [],
                    });
                }

                // 共通のエラー応答（api-design.md §3 末尾）
                operation.Responses ??= [];
                var isSystemInfo = context.Description.ActionDescriptor is ControllerActionDescriptor { ActionName: "GetSystemInfo" };
                if (!allowsAnonymous)
                {
                    AddProblem(operation, context, "401", "ログインが必要です（auth.unauthenticated）。");
                    var exempt = metadata.OfType<AllowWhenPasswordChangeRequiredAttribute>().Any();
                    if (!exempt)
                    {
                        AddProblem(operation, context, "403", "権限がない（access.forbidden）、またはパスワードの変更が必要（auth.password_change_required）。");
                    }
                }

                if (!isSystemInfo)
                {
                    AddProblem(operation, context, "429", "短時間にリクエストが集中しました（rate_limit.exceeded）。");
                }

                AddProblem(operation, context, "500", "処理中にエラーが発生しました（server.error）。");

                if (metadata.OfType<RequiredPermissionAttribute>().FirstOrDefault() is { } permission)
                {
                    operation.Description = $"{operation.Description}\n\n必要な権限: {permission.Description}".Trim();
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-required-permission"] = new JsonNodeExtension(JsonValue.Create(permission.Permission)!);
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static void AddProblem(OpenApiOperation operation, OpenApiOperationTransformerContext context, string status, string description)
    {
        if (operation.Responses!.ContainsKey(status))
        {
            return;
        }

        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemJson] = new() { Schema = new OpenApiSchemaReference("ProblemDetails", context.Document) },
            },
        };
    }
}
