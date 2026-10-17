# syntax=docker/dockerfile:1
# TaskManager API のイメージ（docs/design/architecture.md §7）
#   target: migrate … EF Core マイグレーションバンドルを実行して終了
#   target: runtime … API 本体

FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json .editorconfig ./
COPY src/TaskManager.Domain/TaskManager.Domain.csproj src/TaskManager.Domain/
COPY src/TaskManager.Application/TaskManager.Application.csproj src/TaskManager.Application/
COPY src/TaskManager.Infrastructure/TaskManager.Infrastructure.csproj src/TaskManager.Infrastructure/
COPY src/TaskManager.Api/TaskManager.Api.csproj src/TaskManager.Api/
RUN dotnet restore src/TaskManager.Api/TaskManager.Api.csproj && dotnet tool restore
COPY src/ src/
# コンテナ内のビルドではビルド時の OpenAPI 文書生成を行わない（リポジトリの docs/api に保存済みのものを使う）
RUN dotnet publish src/TaskManager.Api/TaskManager.Api.csproj -c Release -o /app/publish --no-restore \
      -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

FROM build AS migrations-build
RUN dotnet ef migrations bundle \
      --project src/TaskManager.Infrastructure/TaskManager.Infrastructure.csproj \
      --startup-project src/TaskManager.Infrastructure/TaskManager.Infrastructure.csproj \
      --configuration Release --output /app/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS migrate
WORKDIR /app
COPY --from=migrations-build /app/efbundle ./efbundle
USER $APP_UID
# 接続文字列は環境変数 MIGRATOR_CONNECTION で渡す（Secret をイメージに含めない）
ENTRYPOINT ["/bin/sh", "-c", "exec ./efbundle --connection \"$MIGRATOR_CONNECTION\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
# ヘルスチェック用に curl のみ追加
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/taskmanager/attachments \
    && chown "$APP_UID" /var/lib/taskmanager/attachments
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
