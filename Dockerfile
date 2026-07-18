# Playwright .NET image includes browser dependencies
FROM mcr.microsoft.com/playwright/dotnet:v1.52.0-jammy AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY InstagramStoryArchiver.sln ./
COPY src/InstagramStoryArchiver.Domain/InstagramStoryArchiver.Domain.csproj src/InstagramStoryArchiver.Domain/
COPY src/InstagramStoryArchiver.Application/InstagramStoryArchiver.Application.csproj src/InstagramStoryArchiver.Application/
COPY src/InstagramStoryArchiver.Infrastructure/InstagramStoryArchiver.Infrastructure.csproj src/InstagramStoryArchiver.Infrastructure/
COPY src/InstagramStoryArchiver.Worker/InstagramStoryArchiver.Worker.csproj src/InstagramStoryArchiver.Worker/
RUN dotnet restore src/InstagramStoryArchiver.Worker/InstagramStoryArchiver.Worker.csproj
COPY src/ ./src/
RUN dotnet publish src/InstagramStoryArchiver.Worker/InstagramStoryArchiver.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/playwright/dotnet:v1.52.0-jammy AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV DOTNET_ENVIRONMENT=Docker
ENV Instagram__Headless=true

# Exit code 2 = session/challenge requiring manual login. Compose uses unless-stopped;
# the app stops cleanly on session expiry so operators must refresh storage state.
COPY --from=build /app/publish .
RUN mkdir -p /app/data /app/archive /app/logs /app/data/tmp

VOLUME ["/app/data", "/app/archive", "/app/logs"]

ENTRYPOINT ["dotnet", "InstagramStoryArchiver.Worker.dll"]
