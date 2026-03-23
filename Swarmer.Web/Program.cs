using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Swarmer.Domain;
using Swarmer.Domain.Data;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Models;
using Swarmer.Domain.Twitch;
using TwitchLib.Api;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Web;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Sentry(o =>
                    {
                        o.Dsn = "https://2ff402b0e3df4a9aa8a42ed61d8343b9@o1402334.ingest.sentry.io/6734470";
                        o.MinimumEventLevel = LogEventLevel.Information;
                        o.TracesSampleRate = 0.5;
                        o.Environment = context.HostingEnvironment.EnvironmentName;
                    });
            });

            Log.Information("Starting Swarmer.Web");

            // Add services
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddOpenApi();

            // Add Swarmer services
            IConfigurationSection configSection = builder.Configuration.GetSection(nameof(SwarmerConfig));
            builder.Services
                .Configure<SwarmerConfig>(configSection)
                .AddDbContext<AppDbContext>(options =>
                {
                    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                    options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "swarmer"));
                })
                .AddScoped<SwarmerRepository>()
                .AddSingleton(new DiscordSocketConfig
                {
                    DefaultRetryMode = RetryMode.AlwaysRetry,
                    UseInteractionSnowflakeDate = false,
                })
                .AddSingleton<IDiscordService, DiscordService>()
                .AddSingleton<SwarmerDiscordClient>()
                .AddSingleton<StreamProvider>()
                .AddSingleton<ITwitchAPI, TwitchAPI>()
                .AddHostedService<StreamRefresherService>()
                .AddHostedService<StreamsPostingService>()
                .AddHostedService<KeepAppAliveService>()
                .AddHttpClient("KeepAlive", c =>
                {
                    c.Timeout = TimeSpan.FromSeconds(10);
                });

            WebApplication app = builder.Build();

            SwarmerDiscordClient discordClient = app.Services.GetRequiredService<SwarmerDiscordClient>();
            await discordClient.InitAsync();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.MapRazorPages();
            app.MapControllers();
            app.MapDefaultControllerRoute();

            // Map API endpoints
            app.MapGet("/streams", ([FromServices] StreamProvider provider, string? gameName = null) =>
            {
                if (provider.Streams is null || gameName is null)
                {
                    return provider.Streams;
                }

                return gameName.ToLower() switch
                {
                    "devil daggers" => Array.FindAll(provider.Streams,
                        stream => stream.GameName.Equals("devil daggers", StringComparison.OrdinalIgnoreCase)),
                    "hyper demon" => Array.FindAll(provider.Streams,
                        stream => stream.GameName.Equals("hyper demon", StringComparison.OrdinalIgnoreCase)),
                    _ => provider.Streams
                };
            });

            // Add Scalar API documentation
            app.MapOpenApi();
            app.MapScalarApiReference(opt =>
            {
                opt.Title = "Swarmer API";
            });

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
