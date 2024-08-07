# Swarmer[^1]

A Discord bot that notifies when [Devil Daggers](https://store.steampowered.com/app/422970/Devil_Daggers/) streams go live.

### Framework
- .NET 8.0

### Language
- C# 12.0

### Architecture
There was an attempt at following [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html), where:

* [Swarmer.Domain](Swarmer.Domain) represents the core logic of the application, this includes communication with Discord, Twitch API, and the database.


* [Swarmer.Web.Client](Swarmer.Web.Client) represents the Web/UI part that the user interacts with. It is a Blazor WebAssembly project built with [tailwindcss](https://tailwindcss.com/).


* [Swarmer.Web.Server](Swarmer.Web.Server) is where the API is defined and can be regarded as the entry point of the application.

[^1]: This bot was originally made by Angrevol#3416 and this project is a complete rewrite in C#.
