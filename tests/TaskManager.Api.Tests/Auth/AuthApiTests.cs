using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Tests.Support;
using TaskManager.Testing;

namespace TaskManager.Api.Tests.Auth;

/// <summary>認証（authentication-authorization.md §2〜§3、security-design.md）。</summary>
public class AuthApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static HttpRequestMessage SameOriginPost(string url, string origin = "https://localhost")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static string SetCookieHeader(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tm_refresh=", StringComparison.Ordinal));

    [Fact]
    public async Task ログインに成功するとトークンを返しリフレッシュCookieは安全な属性で設定する()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "Login");
        var body = await JsonAsync(response);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(900, body.GetProperty("expiresIn").GetInt32());
        var cookie = SetCookieHeader(response).ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.Ordinal);
        // リフレッシュトークンは本文に含めない
        var refreshValue = SetCookieHeader(response).Split(';')[0]["tm_refresh=".Length..];
        Assert.DoesNotContain(refreshValue, body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ログイン失敗は存在の有無にかかわらず同じ401()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();

        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "wrong-password-123" }, Ct);
        using var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Unique.Email("nobody"), password = "wrong-password-123" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var a = await JsonAsync(wrong);
        var b = await JsonAsync(unknown);
        Assert.Equal("auth.invalid_credentials", a.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
        Assert.Equal("メールアドレスまたはパスワードが正しくありません。", a.GetProperty("title").GetString());
    }

    [Fact]
    public async Task リフレッシュCookieでトークンを更新し使用済みCookieの再利用は失効させる()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        var firstCookie = SetCookieHeader(login).Split(';')[0];

        using var refreshed = await client.SendAsync(SameOriginPost("/api/v1/auth/refresh"), Ct);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        await OpenApiContract.AssertResponseAsync(refreshed, "RefreshAccessToken");
        Assert.NotEqual(firstCookie, SetCookieHeader(refreshed).Split(';')[0]);

        // 盗まれた古い Cookie を別の端末が使う
        using var attacker = factory.CreateAnonymousClient();
        using var replay = SameOriginPost("/api/v1/auth/refresh");
        replay.Headers.Add("Cookie", firstCookie);
        using var replayed = await attacker.SendAsync(replay, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
        Assert.Equal("auth.session_expired", (await JsonAsync(replayed)).GetProperty("code").GetString());

        // 正規の利用者の Cookie も同じ系列なので失効している
        using var legit = await client.SendAsync(SameOriginPost("/api/v1/auth/refresh"), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, legit.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example.test", null)]
    [InlineData("null", null)]
    [InlineData(null, "cross-site")]
    [InlineData(null, null)]
    public async Task 別オリジンからのリフレッシュとログアウトは403(string? origin, string? fetchSite)
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        foreach (var url in new[] { "/api/v1/auth/refresh", "/api/v1/auth/logout" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (origin is not null)
            {
                request.Headers.Add("Origin", origin);
            }

            if (fetchSite is not null)
            {
                request.Headers.Add("Sec-Fetch-Site", fetchSite);
            }

            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("auth.csrf_rejected", (await JsonAsync(response)).GetProperty("code").GetString());
        }

        // 拒否されたあとも Cookie は有効なまま
        using var ok = await client.SendAsync(SameOriginPost("/api/v1/auth/refresh"), Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Originがなく同一オリジンのSecFetchSiteなら許可する()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ログアウトするとCookieを削除しリフレッシュできなくなる()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        var cookie = SetCookieHeader(login).Split(';')[0];

        using var logout = await client.SendAsync(SameOriginPost("/api/v1/auth/logout"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=", SetCookieHeader(logout), StringComparison.OrdinalIgnoreCase);

        using var other = factory.CreateAnonymousClient();
        using var replay = SameOriginPost("/api/v1/auth/refresh");
        replay.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.SendAsync(replay, Ct)).StatusCode);
    }

    [Fact]
    public async Task トークンなしは401で日本語のProblemDetailsを返す()
    {
        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/api/v1/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await JsonAsync(response);
        Assert.Equal("auth.unauthenticated", body.GetProperty("code").GetString());
        Assert.Equal("ログインが必要です。", body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    public static TheoryData<string> TamperedTokens() => ["改ざん", "署名なし", "期限切れ", "別のaudience", "別のissuer", "別の鍵", "形式不正"];

    [Theory]
    [MemberData(nameof(TamperedTokens))]
    public async Task 不正なトークンは401(string kind)
    {
        var user = await factory.Data.CreateUserAsync();
        var valid = await factory.IssueTokenAsync(user);
        var token = kind switch
        {
            "改ざん" => TamperPayload(valid),
            "署名なし" => UnsignedToken(user.Id),
            "期限切れ" => SignedToken(user.Id, factory.SigningKey, expires: DateTime.UtcNow.AddMinutes(-5)),
            "別のaudience" => SignedToken(user.Id, factory.SigningKey, audience: "other-app"),
            "別のissuer" => SignedToken(user.Id, factory.SigningKey, issuer: "other-issuer"),
            "別の鍵" => SignedToken(user.Id, Convert.ToBase64String(new byte[48])),
            _ => "not-a-jwt",
        };
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/v1/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.unauthenticated", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task 正しく署名された有効なトークンは受け付ける()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.IssueTokenAsync(user));

        using var response = await client.GetAsync("/api/v1/me", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "GetMe");
    }

    [Fact]
    public async Task パスワードを変更すると変更前のアクセストークンは使えなくなる()
    {
        var user = await factory.Data.CreateUserAsync();
        using var otherDevice = await factory.CreateClientAsync(user);
        using var client = await factory.CreateClientAsync(user);

        using var changed = await client.PostAsJsonAsync("/api/v1/auth/password",
            new { currentPassword = user.Password, newPassword = "Kanagawa-Minato-2026" }, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await OpenApiContract.AssertResponseAsync(changed, "ChangePassword");

        using var stale = await otherDevice.GetAsync("/api/v1/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);

        var newToken = (await JsonAsync(changed)).GetProperty("accessToken").GetString();
        using var fresh = factory.CreateAnonymousClient();
        fresh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task 利用停止されるとアクセストークンは即座に使えなくなる()
    {
        var admin = await factory.Data.CreateUserAsync(administrator: true);
        var user = await factory.Data.CreateUserAsync();
        using var userClient = await factory.CreateClientAsync(user);
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/v1/me", Ct)).StatusCode);

        using var adminClient = await factory.CreateClientAsync(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/v1/users/{user.Id}/deactivate", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task 一時パスワード状態では変更以外の操作は403()
    {
        var user = await factory.Data.CreateUserAsync(mustChangePassword: true);
        using var client = await factory.CreateClientAsync(user);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me", Ct)).StatusCode);
        using var projects = await client.GetAsync("/api/v1/projects", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, projects.StatusCode);
        Assert.Equal("auth.password_change_required", (await JsonAsync(projects)).GetProperty("code").GetString());

        using var changed = await client.PostAsJsonAsync("/api/v1/auth/password",
            new { currentPassword = user.Password, newPassword = "Kanagawa-Minato-2026" }, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.False((await JsonAsync(changed)).GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task ログインは同じIPから1分に10回までで11回目は429とRetryAfter()
    {
        using var client = factory.CreateAnonymousClient(remoteIp: "192.0.2.111");
        for (var i = 0; i < 10; i++)
        {
            using var attempt = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Unique.Email("rate"), password = "wrong-password-123" }, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        using var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Unique.Email("rate"), password = "wrong-password-123" }, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter is not null || limited.Headers.Contains("Retry-After"));
        Assert.Equal("rate_limit.exceeded", (await JsonAsync(limited)).GetProperty("code").GetString());
        await OpenApiContract.AssertResponseAsync(limited, "Login");

        // 別の IP は影響を受けない
        using var other = factory.CreateAnonymousClient(remoteIp: "192.0.2.112");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await other.PostAsJsonAsync("/api/v1/auth/login", new { email = Unique.Email("rate"), password = "wrong-password-123" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task パスワード変更は利用者ごとに1分5回までで6回目は429()
    {
        var user = await factory.Data.CreateUserAsync();
        for (var i = 0; i < 5; i++)
        {
            // 毎回別の端末（送信元 IP）から、同じ利用者のトークンで試す
            using var attempt = await factory.CreateClientAsync(user);
            using var response = await attempt.PostAsJsonAsync("/api/v1/auth/password",
                new { currentPassword = "wrong-password-123", newPassword = "Kanagawa-Minato-2026" }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var client = await factory.CreateClientAsync(user);
        using var limited = await client.PostAsJsonAsync("/api/v1/auth/password",
            new { currentPassword = "wrong-password-123", newPassword = "Kanagawa-Minato-2026" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        await OpenApiContract.AssertResponseAsync(limited, "ChangePassword");
    }

    [Fact]
    public async Task 信頼しない接続元からのX_Forwarded_Forではレート制限を回避できない()
    {
        // 公開 IP（文書用アドレス）から直接接続し、X-Forwarded-For を毎回変えて送る
        using var client = factory.CreateAnonymousClient(remoteIp: "203.0.113.50");
        for (var i = 0; i < 10; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new { email = Unique.Email("xff"), password = "wrong-password-123" }),
            };
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{i + 1}");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request, Ct)).StatusCode);
        }

        using var spoofed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = Unique.Email("xff"), password = "wrong-password-123" }),
        };
        spoofed.Headers.Add("X-Forwarded-For", "198.51.100.99");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(spoofed, Ct)).StatusCode);
    }

    [Fact]
    public async Task 信頼するネットワークの中継からはX_Forwarded_Forの利用者IPで区別する()
    {
        // nginx（プライベート IP）経由: 利用者ごとに別の区分になる
        using var proxy = factory.CreateAnonymousClient(remoteIp: "172.18.0.5");
        for (var i = 0; i < 11; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new { email = Unique.Email("proxy"), password = "wrong-password-123" }),
            };
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{100 + i}");
            Assert.Equal(HttpStatusCode.Unauthorized, (await proxy.SendAsync(request, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task ログインに5回失敗するとロックされ正しいパスワードでも401_locked()
    {
        var user = await factory.Data.CreateUserAsync();
        using var client = factory.CreateAnonymousClient();
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "wrong-password-123" }, Ct);
        }

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.locked", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ログイン中のログにパスワードとトークンを出さない()
    {
        var user = await factory.Data.CreateUserAsync(password: "Unique-Log-Check-Pw-2026");
        using var client = factory.CreateAnonymousClient();
        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Wrong-Log-Check-Pw-2026" }, Ct);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        var token = (await JsonAsync(login)).GetProperty("accessToken").GetString()!;
        var refresh = SetCookieHeader(login).Split(';')[0]["tm_refresh=".Length..];

        var logs = string.Join('\n', factory.Logs);
        Assert.Contains("LoginSucceeded", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Unique-Log-Check-Pw-2026", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Wrong-Log-Check-Pw-2026", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(token, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(refresh, logs, StringComparison.Ordinal);
    }

    private static string SignedToken(Guid userId, string base64Key, DateTime? expires = null, string audience = "taskmanager-web", string issuer = "taskmanager")
    {
        var now = DateTime.UtcNow;
        var exp = expires ?? now.AddMinutes(10);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("stamp", Guid.NewGuid().ToString())]),
            IssuedAt = exp.AddMinutes(-15),
            NotBefore = exp.AddMinutes(-15),
            Expires = exp,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(base64Key)), SecurityAlgorithms.HmacSha256),
        });
    }

    private static string UnsignedToken(Guid userId)
    {
        static string Encode(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        return Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." +
               Encode($"{{\"sub\":\"{userId}\",\"iss\":\"taskmanager\",\"aud\":\"taskmanager-web\",\"exp\":{exp},\"role\":\"administrator\"}}") + ".";
    }

    private static string TamperPayload(string token)
    {
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        var tampered = payload.Replace("\"member\"", "\"administrator\"", StringComparison.Ordinal);
        Assert.NotEqual(payload, tampered);
        return $"{parts[0]}.{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(tampered))}.{parts[2]}";
    }
}
