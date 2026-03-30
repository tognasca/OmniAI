# ── Build stage ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/OmniAI.API/OmniAI.API.csproj",                     "src/OmniAI.API/"]
COPY ["src/OmniAI.Application/OmniAI.Application.csproj",     "src/OmniAI.Application/"]
COPY ["src/OmniAI.Infrastructure/OmniAI.Infrastructure.csproj","src/OmniAI.Infrastructure/"]
COPY ["src/OmniAI.Domain/OmniAI.Domain.csproj",               "src/OmniAI.Domain/"]
RUN dotnet restore "src/OmniAI.API/OmniAI.API.csproj"

COPY . .
WORKDIR "/src/src/OmniAI.API"
RUN dotnet build "OmniAI.API.csproj" -c Release -o /app/build

# ── Publish stage ─────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "OmniAI.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

RUN adduser --disabled-password --gecos '' appuser \
    && mkdir -p /app/keys /app/logs \
    && chown -R appuser:appuser /app

USER appuser

COPY --from=publish --chown=appuser:appuser /app/publish .

ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "OmniAI.API.dll"]
