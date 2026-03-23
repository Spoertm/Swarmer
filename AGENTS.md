# Swarmer - Agent Guide

## Project Overview

Swarmer is a Discord bot that notifies when [Devil Daggers](https://store.steampowered.com/app/422970/Devil_Daggers/) and HYPER DEMON Twitch streams go live. The project also exposes a REST API as an alternative to Twitch's API for querying streams of these specific games.

This project was originally created by Angrevol#3416 and has been completely rewritten in C#.

## Technology Stack

- **Framework**: .NET 10.0
- **Language**: C# 14.0
- **Database**: PostgreSQL with Entity Framework Core 10.0
- **Frontend**: ASP.NET MVC with Razor views and Tailwind CSS v4
- **API Documentation**: Scalar (modern Swagger alternative)
- **Discord Integration**: Discord.Net 3.19
- **Twitch Integration**: TwitchLib.Api 3.10
- **Logging**: Serilog with Sentry integration
- **Testing**: xUnit with NSubstitute for mocking

## Project Structure

The solution follows [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) with three projects:

### 1. Swarmer.Domain (Class Library)

Core business logic and external integrations.

```
Swarmer.Domain/
├── Data/                 # Entity Framework Core context and repositories
│   ├── AppDbContext.cs
│   ├── BannedUser.cs
│   ├── GameChannel.cs
│   ├── StreamMessage.cs
│   ├── SwarmerRepository.cs
│   └── Migrations/       # EF Core migrations
├── Discord/              # Discord bot integration
│   ├── DiscordService.cs
│   ├── IDiscordService.cs
│   └── SwarmerDiscordClient.cs
├── Extensions/           # Extension methods
│   ├── EmbedExtensions.cs
│   └── StringExtensions.cs
├── Models/               # Domain models
│   ├── SwarmerConfig.cs
│   ├── Result.cs
│   └── RepeatingBackgroundService.cs
├── Twitch/               # Twitch API integration
│   ├── StreamProvider.cs
│   ├── StreamRefresherService.cs
│   ├── StreamsPostingService.cs
│   └── StreamToPost.cs
├── KeepAppAliveService.cs
└── Swarmer.Domain.csproj
```

### 2. Swarmer.Web (ASP.NET Core MVC)

Entry point of the application. Serves the web UI using ASP.NET MVC with Razor views and exposes REST API endpoints.

```
Swarmer.Web/
├── Controllers/
│   └── HomeController.cs       # MVC controllers
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml        # Landing page with hero, features, API demo
│   │   └── Privacy.cshtml      # Privacy policy
│   ├── Shared/
│   │   └── _Layout.cshtml      # Main layout with nav, footer, dark mode
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── Styles/
│   └── input.css               # Tailwind source styles
├── wwwroot/
│   ├── css/
│   │   └── site.css            # Generated Tailwind CSS
│   └── images/                 # Logo and assets
├── Properties/
│   └── launchSettings.json
├── Program.cs                  # Application bootstrap and DI configuration
├── appsettings.Development.json
├── appsettings.Production.json
├── package.json                # npm scripts for Tailwind
├── tailwind.config.js          # Tailwind configuration
├── Dockerfile                  # Docker deployment configuration
└── Swarmer.Web.csproj
```

### 3. Swarmer.Tests (xUnit Test Project)

Unit tests for the domain layer.

```
Swarmer.Tests/
├── UnitTests/
│   ├── StreamMock.cs
│   └── SwarmerRepositoryTests.cs
├── appsettings.Testing.json
└── Swarmer.Tests.csproj
```

## Build and Test Commands

### Build the Solution

```bash
dotnet build Swarmer.slnx
```

### Run Tests

```bash
dotnet test Swarmer.slnx
```

### Run the Application (Development)

**Option 1: Just run the app (CSS is built automatically during build)**
```bash
cd Swarmer.Web
dotnet run
```

**Option 2: With CSS hot reload (recommended for UI development)**

Terminal 1 - Watch and rebuild CSS on changes:
```bash
cd Swarmer.Web
npm run build-css
```

Terminal 2 - Run the app with hot reload:
```bash
cd Swarmer.Web
dotnet watch run
```

The application will be available at:
- Web UI: https://localhost:5001 or http://localhost:5000
- Scalar API docs: https://localhost:5001/scalar/v1

### Docker Build

```bash
cd Swarmer.Web
docker build -f Dockerfile -t swarmer ..
```

### Database Migrations

Since the project uses EF Core Migrations, you may need to apply them:

```bash
cd Swarmer.Web
dotnet ef database update
```

To create a new migration:

```bash
cd Swarmer.Domain
dotnet ef migrations add <MigrationName> --startup-project ../Swarmer.Web
```

## Frontend Development

### Tailwind CSS Workflow

The project uses **Tailwind CSS v4** with npm-based processing:

1. **Development** - Watch mode with hot reload:
   ```bash
   npm run build-css
   ```

2. **Production** - Minified build:
   ```bash
   npm run build-css-prod
   ```

CSS is also automatically built during the .NET build process via the `BuildTailwind` MSBuild target in `Swarmer.Web.csproj`.

### Key UI Features

- **Dark Mode**: Class-based (`dark:`) with localStorage persistence
- **Discord Simulation**: Animated stream notifications in the hero section
- **Responsive Design**: Mobile-first with Tailwind breakpoints

### File Locations

- **Tailwind source**: `Styles/input.css`
- **Generated CSS**: `wwwroot/css/site.css` (do not edit directly)
- **Views**: `Views/` folder with `.cshtml` files
- **Config**: `tailwind.config.js`

## Code Style Guidelines

The project uses `.editorconfig` with the following key conventions:

### Indentation
- **Space** indentation (size 4)
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
- SA1600: Elements should be documented
- SA0001: XML comment analysis is disabled due to project configuration

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
- **ASP.NET Core MVC Testing** for integration tests

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
    "DefaultConnection": "Host=localhost;Database=postgres;Username=postgres;Password=postgres"
  },
  "SwarmerConfig": {
    "BotToken": "discord-bot-token",
    "ClientId": "twitch-client-id",
    "ClientSecret": "twitch-client-secret"
  }
}
```

The Twitch Access Token is automatically requested via OAuth2 client credentials flow on startup - it does not need to be configured manually.

### Environment-Specific Files

- **Development**: `appsettings.Development.json` - Local development settings
- **Production**: `appsettings.Production.json` - Production deployment settings
- **Testing**: `appsettings.Testing.json` - Test configuration with dummy values

### Twitch Games Monitored

The bot monitors Twitch streams for:
- Devil Daggers (Game ID: 490905)
- HYPER DEMON (Game ID: 1350012934)

## Architecture Patterns

### Background Services
The application uses three hosted background services:

1. **StreamRefresherService** - Polls Twitch API every minute to refresh stream data and obtain OAuth tokens
2. **StreamsPostingService** - Posts new streams to Discord and manages stream state transitions (30-second interval)
3. **KeepAppAliveService** - Keeps the application alive (for hosting platforms that require activity)

All background services inherit from `RepeatingBackgroundService` which provides:
- Periodic execution with `PeriodicTimer`
- Exception handling and logging
- Graceful cancellation

### Banned Users
Banned Twitch user logins are stored in the `BannedUsers` database table. Streams from banned users are filtered out before being posted to Discord. To ban a user, insert their Twitch login into the `BannedUsers` table.

### Repository Pattern
Data access is abstracted through repository classes:
- `SwarmerRepository` - Stream message and game channel operations

### API Endpoints
REST API is defined in `Program.cs` using Minimal API pattern:
- `GET /streams` - Get all streams or filter by game name (e.g., `?game=devil+daggers`)

## Security Considerations

### Sensitive Configuration
- **Never commit real API tokens** - The `appsettings.Development.json` and `appsettings.Production.json` contain real tokens but are gitignored from public repositories
- In production, tokens are loaded from environment-specific configuration files

### CORS
The API allows any origin (`AllowAnyOrigin`) for the `/streams` endpoint.

## Deployment

### Docker
The Dockerfile is located in `Swarmer.Web/Dockerfile`:
- Uses multi-stage build
- Based on `mcr.microsoft.com/dotnet/aspnet:10.0`
- Exposes ports 80 and 443

### Database
PostgreSQL is required. The application uses EF Core migrations for schema management. Migrations history is stored in the `swarmer` schema (`__EFMigrationsHistory` table).

## Development Workflow

1. Make changes to the codebase
2. Run tests: `dotnet test`
3. Build: `dotnet build`
4. For UI changes, run Tailwind watch: `npm run build-css`
5. Create migrations if database schema changes: `dotnet ef migrations add <Name> --startup-project ../Swarmer.Web`
6. Test locally with `dotnet watch run`

## Useful Notes

- The project uses file-scoped namespaces (C# 10+)
- Nullable reference types are enabled across all projects
- Implicit usings are enabled
- Dark mode is supported in the UI and persists via local storage
- The Discord simulation on the home page uses vanilla JavaScript for animations
- The `BannedUser` entity stores `UserLogin` (Twitch login name) for filtering banned streamers
- Stream messages in Discord use embeds that transition between "online" and "offline" states
- A "lingering" mechanism prevents rapid Discord message flip-flopping when Twitch API returns inconsistent data
