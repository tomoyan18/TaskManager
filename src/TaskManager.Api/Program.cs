using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using TaskManager.Api.Background;
using TaskManager.Api.Configuration;
using TaskManager.Api.Errors;
using TaskManager.Api.Health;
using TaskManager.Api.Http;
using TaskManager.Api.Middleware;
using TaskManager.Api.OpenApi;
using TaskManager.Api.Security;
using TaskManager.Application;
using TaskManager.Application.Abstractions;
using TaskManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ビルド時の OpenAPI 文書生成（Microsoft.Extensions.ApiDescription.Server）で起動された場合は true
var isDocumentGeneration = string.Equals(
    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.Ordinal);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1 * 1024 * 1024; // JSON は 1MB まで。添付は個別に 11MB（security-design.md S-10, S-11）
});

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
}

void ConfigureJson(JsonSerializerOptions json)
{
    json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    json.MaxDepth = 32;
    // 数値を文字列で受け付けない（OpenAPI のスキーマも integer のみになる）
    json.NumberHandling = JsonNumberHandling.Strict;
}

builder.Services
    .AddControllers(options =>
    {
        // 応答は JSON のみ（docs/design/api-design.md §1）
        options.Filters.Add(new ProducesAttribute("application/json"));
        options.Filters.Add<PasswordChangeRequiredFilter>();
        options.Filters.Add<SameOriginFilter>();
        // 検証エラーのキーを JSON のプロパティ名（camelCase）にする
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustNotBeNullAccessor(_ => "値を入力してください。");
        messages.SetAttemptedValueIsInvalidAccessor((_, _) => "入力の形式が正しくありません。");
        messages.SetValueIsInvalidAccessor(_ => "入力の形式が正しくありません。");
        messages.SetValueMustBeANumberAccessor(_ => "数値を入力してください。");
        messages.SetMissingBindRequiredValueAccessor(_ => "値を入力してください。");
        messages.SetMissingKeyOrValueAccessor(() => "値を入力してください。");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "リクエストの本文がありません。");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "入力の形式が正しくありません。");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "入力の形式が正しくありません。");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "数値を入力してください。");
        messages.SetUnknownValueIsInvalidAccessor(_ => "入力の形式が正しくありません。");
    })
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create)
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TaskManagerExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddTaskManagerOpenApi();
builder.Services.AddTaskManagerAuthentication(builder.Configuration, isDocumentGeneration);
builder.Services.AddTaskManagerRateLimiting();
builder.Services.AddOptions<AuthCookieOptions>().Bind(builder.Configuration.GetSection(AuthCookieOptions.SectionName));
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<SecurityStampValidator>();
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddSingleton<RefreshCookie>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
if (!isDocumentGeneration && builder.Configuration.GetValue("Maintenance:Enabled", true))
{
    builder.Services.AddHostedService<MaintenanceWorker>();
}

// web（nginx）経由の場合、利用者の IP とスキームを X-Forwarded-* から取得する。
// X-Forwarded-* を信頼するのは、接続元が信頼するネットワーク（既定: プライベート IP と loopback。Docker の内部ネットワークを含む）の
// 場合だけにする。API を直接公開しても、外部の利用者が送信元 IP を偽ってレート制限を回避できない（Phase 7 で修正）。
// 変更する場合は ForwardedHeaders__KnownNetworks__0=10.0.0.0/8 のように指定する。
var trustedNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()
    ?? ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128", "fc00::/7"];
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var network in trustedNetworks)
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }

    options.ForwardLimit = 1;
});

var app = builder.Build();

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Swagger UI は静的ファイルを返すミドルウェアのため、既定の認可ポリシー（ログイン必須）より前に置く。
// 公開の可否は Swagger:Enabled（環境変数 Swagger__Enabled）で制御する（security-design.md S-20）。
if (swaggerEnabled)
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint($"/openapi/{OpenApiSetup.DocumentName}.json", "TaskManager API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "TaskManager API";
    });
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous().DisableRateLimiting();

if (swaggerEnabled || isDocumentGeneration)
{
    app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
}

if (!isDocumentGeneration)
{
    await StartupTasks.RunAsync(app);
}

await app.RunAsync();

/// <summary>WebApplicationFactory（API テスト）から参照するための宣言。</summary>
public partial class Program;
