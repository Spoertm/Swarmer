# Swarmer[^1]

A Discord bot that notifies when [Devil Daggers](https://store.steampowered.com/app/422970/Devil_Daggers/) and [HYPER DEMON](https://store.steampowered.com/app/1743850/HYPER_DEMON/) streams go live on Twitch.

<div align="center">
  <img src="https://github.com/user-attachments/assets/6453c201-7370-43d7-97b6-d3bc91dd2afe" width="500" />
</div>

## Quick Start

```bash
# Build
dotnet build Swarmer.slnx

# Test
dotnet test

# Run (requires PostgreSQL + API credentials)
cd Swarmer.Web
dotnet run
```

The app will be available at `http://localhost:5000` (or `https://localhost:5001`).

## API

Query live streams without hitting Twitch's API directly:

```
GET /streams?game=devil+daggers
```

Returns active streams for Devil Daggers or HYPER DEMON.

## Architecture

| Project          | Purpose                                      |
| ---------------- | -------------------------------------------- |
| `Swarmer.Domain` | Core logic—Discord bot, Twitch API, database |
| `Swarmer.Web`    | ASP.NET MVC frontend + REST API entry point  |
| `Swarmer.Tests`  | Unit tests                                   |

Built with .NET 10, C# 14, EF Core + PostgreSQL, Tailwind CSS.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL
- [Discord bot token](https://discord.com/developers/applications)
- [Twitch API credentials](https://dev.twitch.tv/)

Copy `Swarmer.Web/appsettings.Example.json` → `Swarmer.Web/appsettings.Development.json` and fill in real credentials.

[^1]: This bot was originally made by Angrevol#3416 and this project is a complete rewrite in C#.
