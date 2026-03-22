# Swarmer - Agent Guide

## Project Overview

Swarmer is a Discord bot that notifies when [Devil Daggers](https://store.steampowered.com/app/422970/Devil_Daggers/) and HYPER DEMON Twitch streams go live. The project also exposes a REST API as an alternative to Twitch's API for querying streams of these specific games.

This project was originally created by Angrevol#3416 and has been completely rewritten in C#.

## Technology Stack

- **Framework**: .NET 10.0
- **Language**: C# 14.0
- **Database**: PostgreSQL with Entity Framework Core 10.0
- **Frontend**: Blazor WebAssembly with Tailwind CSS
- **API Documentation**: Swagger (Swashbuckle)
- **Discord Integration**: Discord.Net 3.19
- **Twitch Integration**: TwitchLib.Api 3.10
- **Logging**: Serilog with Sentry integration
- **Testing**: xUnit with NSubstitute for mocking

## Project Structure

The solution follows [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) with four projects:

### 1. Swarmer.Domain (Class Library)

Core business logic and external integrations.

```
Swarmer.Domain/
├── Database/           # Entity Framework Core context and repositories
│   ├── AppDbContext.cs
│   ├── GameChannel.cs
│   ├── StreamMessage.cs
│   ├── ConfigurationEntity.cs
│   ├── SwarmerRepository.cs
│   ├── ConfigRepository.cs
│   └── Migrations/     # EF Core migrations
├── Discord/            # Discord bot integration
│   ├── DiscordService.cs
│   ├── IDiscordService.cs
│   └── SwarmerDiscordClient.cs
├── Extensions/         # Extension methods
│   ├── EmbedExtensions.cs
│   └── StringExtensions.cs
├── Models/             # Domain models
│   ├── SwarmerConfig.cs
│   ├── Result.cs
│   └── RepeatingBackgroundService.cs
└── Twitch/             # Twitch API integration
    ├── StreamProvider.cs
    ├── StreamRefresherService.cs
    ├── StreamsPostingService.cs
    └── StreamToPost.cs
```

### 2. Swarmer.Web.Server (ASP.NET Core Web)

Entry point of the application. Hosts the Blazor WebAssembly client and exposes REST API endpoints.

```
Swarmer.Web.Server/
├── Program.cs              # Application bootstrap and DI configuration
├── Endpoints/
│   └── SwarmerEndpoints.cs # API endpoint definitions
├── appsettings.json        # Configuration (connection strings, tokens)
├── appsettings.Development.json
├── Dockerfile              # Docker deployment configuration
└── wwwroot/swagger-ui/     # Swagger UI custom styling
```

### 3. Swarmer.Web.Client (Blazor WebAssembly)

Client-side UI built with Blazor and Tailwind CSS.

```
Swarmer.Web.Client/
├── Program.cs
├── App.razor
├── _Imports.razor
├── Pages/
│   └── Index.razor         # Landing page
├── Shared/
│   ├── MainLayout.razor
│   ├── NavBar.razor
│   ├── LinkButton.razor
│   └── ReloadLink.razor
├── Services/
│   └── DarkModeManager.cs  # Dark mode state management
├── Styles/
│   └── app.css             # Source CSS for Tailwind
├── wwwroot/
│   ├── index.html
│   ├── app.css             # Generated Tailwind CSS
│   └── Assets/             # Images and logos
└── tailwind.config.js      # Tailwind configuration
```

### 4. Swarmer.UnitTests (xUnit Test Project)

Unit tests for the domain layer.

```
Swarmer.UnitTests/
├── SwarmerRepositoryTests.cs
├── SwarmerEndpointsTests.cs
├── StreamMock.cs
└── appsettings.Testing.json
```

## Build and Test Commands

### Build the Solution

```bash
dotnet build Swarmer.sln
```

### Run Tests

```bash
dotnet test Swarmer.sln
```

### Run the Application (Development)

```bash
cd Swarmer.Web.Server
dotnet run
```

The application will be available at:
- Web UI: https://localhost:5001 or http://localhost:5000
- Swagger API docs: https://localhost:5001/swagger

### Docker Build

```bash
cd Swarmer.Web.Server
docker build -f Dockerfile -t swarmer ..
```

### Database Migrations

Since the project uses EF Core Migrations, you may need to apply them:

```bash
cd Swarmer.Web.Server
dotnet ef database update
```

To create a new migration:

```bash
cd Swarmer.Domain
dotnet ef migrations add <MigrationName> --startup-project ../Swarmer.Web.Server
```

## Code Style Guidelines

The project uses `.editorconfig` with the following key conventions:

### Indentation
- **Tab** indentation (size 4)
- Generated code (Migrations, AssemblyInfo) is excluded from analysis

### Naming Conventions
- Private/internal fields: `_camelCase` (prefix with underscore)
- Constants: `PascalCase`
- Avoid `this.` prefix for members

### C# Style
- Use explicit types instead of `var` (built-in types and elsewhere)
- Prefer predefined type keywords (e.g., `string` instead of `String`)
- Using directives placed outside namespaces
- Sort system directives first
- Prefer braces for all control flow statements

### Disabled StyleCop Rules
- SA1633: File should have header
- SA1309: Field names should not begin with underscore
- SA1128: Put constructor initializers on their own line
- SA1209: Using alias directives should be placed after other using directives
- SA1101: Prefix local calls with this
- SA1402: File may only contain a single type

### Static Analysis
The project includes several analyzers:
- Roslynator.Analyzers
- SonarAnalyzer.CSharp
- StyleCop.Analyzers

## Testing Instructions

### Test Framework
- **xUnit** for test framework
- **NSubstitute** for mocking
- **EF Core InMemory** for database testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~SwarmerRepositoryTests"
```

### Test Configuration
Tests use `appsettings.Testing.json` for configuration with dummy values for API tokens.

### Key Test Patterns
- Repository tests use EF Core InMemory database
- Discord service is mocked using NSubstitute
- Each test gets a fresh database instance (GUID-based database name)

## Configuration

### Required Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=postgres;Username=postgres;Password=postgres"
  },
  "SwarmerConfig": {
    "BotToken": "discord-bot-token",
    "ClientId": "twitch-client-id",
    "AccessToken": "twitch-access-token",
    "ClientSecret": "twitch-client-secret",
    "BannedUserLogins": ["user1", "user2"]
  }
}
```

### Environment Variables (Production)

In production, the following environment variables are required:
- `PostgresConnectionString` - PostgreSQL connection string

### Twitch Games Monitored

The bot monitors Twitch streams for:
- Devil Daggers (Game ID: 490905)
- HYPER DEMON (Game ID: 1350012934)

## Architecture Patterns

### Background Services
The application uses three hosted background services:

1. **StreamRefresherService** - Polls Twitch API every minute to refresh stream data
2. **StreamsPostingService** - Posts new streams to Discord and manages stream state transitions
3. **KeepAppAliveService** - Keeps the application alive (for hosting platforms that require activity)

All background services inherit from `RepeatingBackgroundService` which provides:
- Periodic execution with `PeriodicTimer`
- Exception handling and logging
- Graceful cancellation

### Repository Pattern
Data access is abstracted through repository classes:
- `SwarmerRepository` - Stream message and game channel operations
- `ConfigRepository` - Bot configuration management

### Discord Integration
- `SwarmerDiscordClient` - Custom Discord socket client wrapper
- `DiscordService` - Service for sending embeds and managing stream messages

### API Endpoints
REST API is defined in `SwarmerEndpoints.cs` using Minimal API pattern:
- `GET /streams` - Get all streams or filter by game name

## Security Considerations

### Sensitive Configuration
- **Never commit real API tokens** - The `appsettings.json` in the repository contains example/dummy values
- In production, tokens are loaded from:
  - Environment variables (`PostgresConnectionString`)
  - Database configuration (JSON config stored in PostgreSQL)

### Sentry Integration
Error tracking is configured with Sentry (DSN is hardcoded in Program.cs). This captures:
- Exceptions
- Information-level logs
- 50% of transactions (performance tracing)

### CORS
The API allows any origin (`AllowAnyOrigin`) for the `/streams` endpoint.

## Deployment

### Docker
The Dockerfile is located in `Swarmer.Web.Server/Dockerfile`:
- Uses multi-stage build
- Based on `mcr.microsoft.com/dotnet/aspnet:10.0`
- Exposes ports 80 and 443

### Database
PostgreSQL is required. The application uses EF Core migrations for schema management.

## Development Workflow

1. Make changes to the codebase
2. Run tests: `dotnet test`
3. Build: `dotnet build`
4. For UI changes, Tailwind CSS may need regeneration (the executable is included)
5. Create migrations if database schema changes
6. Test locally with `dotnet run`

## Useful Notes

- The project uses file-scoped namespaces (C# 10+)
- Nullable reference types are enabled across all projects
- Implicit usings are enabled
- The Web Client includes a pre-built `tailwindcss-windows-x64.exe` for CSS generation
- Dark mode is supported in the UI and persists via local storage
