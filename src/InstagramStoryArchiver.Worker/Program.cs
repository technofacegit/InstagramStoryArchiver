using System.CommandLine;
using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Entities;
using InstagramStoryArchiver.Domain.Exceptions;
using InstagramStoryArchiver.Infrastructure;
using InstagramStoryArchiver.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Serilog;
using IAppClock = InstagramStoryArchiver.Application.Abstractions.IClock;

var rootCommand = new RootCommand("Instagram Story Archiver");

var loginCommand = new Command("login", "Open Instagram in a visible browser and save storage state after manual login.");
loginCommand.SetHandler(async () =>
{
    Environment.ExitCode = await RunLoginAsync();
});
rootCommand.AddCommand(loginCommand);

var usersCommand = new Command("users", "Manage monitored Instagram users.");
var listCommand = new Command("list", "List monitored users.");
listCommand.SetHandler(async () => Environment.ExitCode = await RunWithHostAsync(async sp =>
{
    var repo = sp.GetRequiredService<IMonitoredUserRepository>();
    var users = await repo.GetAllAsync(CancellationToken.None);
    if (users.Count == 0)
    {
        Console.WriteLine("No monitored users.");
        return;
    }

    foreach (var user in users)
    {
        Console.WriteLine(
            $"{user.Username,-30} active={user.IsActive,-5} next={user.NextCheckAt?.ToString("u") ?? "-"} failures={user.ConsecutiveFailureCount}");
    }
}));
usersCommand.AddCommand(listCommand);

var addUsernameArg = new Argument<string>("username");
var addCommand = new Command("add", "Add a monitored user.") { addUsernameArg };
addCommand.SetHandler(async (string username) =>
{
    Environment.ExitCode = await RunWithHostAsync(async sp =>
    {
        var normalized = UsernameNormalizer.Normalize(username);
        var options = sp.GetRequiredService<IOptions<InstagramOptions>>().Value;
        var repo = sp.GetRequiredService<IMonitoredUserRepository>();
        var clock = sp.GetRequiredService<IAppClock>();
        var existing = await repo.GetByUsernameAsync(normalized, CancellationToken.None);
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.UpdatedAt = clock.UtcNow;
            existing.NextCheckAt ??= clock.UtcNow;
            await repo.UpdateAsync(existing, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
            Console.WriteLine($"Re-enabled existing user '{normalized}'.");
            return;
        }

        var activeCount = await repo.GetActiveCountAsync(CancellationToken.None);
        if (activeCount >= options.MaxMonitoredUsers)
        {
            throw new InvalidOperationException(
                $"Maximum monitored users ({options.MaxMonitoredUsers}) reached.");
        }

        var now = clock.UtcNow;
        await repo.AddAsync(new MonitoredInstagramUser
        {
            Id = Guid.NewGuid(),
            Username = normalized,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            NextCheckAt = now
        }, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);
        Console.WriteLine($"Added user '{normalized}'.");
    });
}, addUsernameArg);
usersCommand.AddCommand(addCommand);

var removeUsernameArg = new Argument<string>("username");
var removeCommand = new Command("remove", "Deactivate and remove a monitored user (story history is kept).") { removeUsernameArg };
removeCommand.SetHandler(async (string username) =>
{
    Environment.ExitCode = await RunWithHostAsync(async sp =>
    {
        var normalized = UsernameNormalizer.Normalize(username);
        var repo = sp.GetRequiredService<IMonitoredUserRepository>();
        var clock = sp.GetRequiredService<IAppClock>();
        var user = await repo.GetByUsernameAsync(normalized, CancellationToken.None)
            ?? throw new InvalidOperationException($"User '{normalized}' not found.");
        user.IsActive = false;
        user.UpdatedAt = clock.UtcNow;
        await repo.UpdateAsync(user, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);
        Console.WriteLine($"Disabled and removed from active monitoring: '{normalized}'. Archived stories were kept.");
    });
}, removeUsernameArg);
usersCommand.AddCommand(removeCommand);

var enableUsernameArg = new Argument<string>("username");
var enableCommand = new Command("enable", "Enable a monitored user.") { enableUsernameArg };
enableCommand.SetHandler(async (string username) =>
{
    Environment.ExitCode = await RunWithHostAsync(async sp =>
    {
        await SetUserActiveAsync(sp, username, true);
        Console.WriteLine($"Enabled '{UsernameNormalizer.Normalize(username)}'.");
    });
}, enableUsernameArg);
usersCommand.AddCommand(enableCommand);

var disableUsernameArg = new Argument<string>("username");
var disableCommand = new Command("disable", "Disable a monitored user without deleting history.") { disableUsernameArg };
disableCommand.SetHandler(async (string username) =>
{
    Environment.ExitCode = await RunWithHostAsync(async sp =>
    {
        await SetUserActiveAsync(sp, username, false);
        Console.WriteLine($"Disabled '{UsernameNormalizer.Normalize(username)}'.");
    });
}, disableUsernameArg);
usersCommand.AddCommand(disableCommand);

var checkUsernameArg = new Argument<string>("username");
var checkCommand = new Command("check", "Check a single monitored user now.") { checkUsernameArg };
checkCommand.SetHandler(async (string username) =>
{
    Environment.ExitCode = await RunWithHostAsync(async sp =>
    {
        var orchestrator = sp.GetRequiredService<IStoryCheckOrchestrator>();
        var result = await orchestrator.CheckUserAsync(username, CancellationToken.None);
        Console.WriteLine(
            $"Checked {result.Username}: activeStories={result.HadActiveStories}, new={result.NewStories}, downloaded={result.Downloaded}, skipped={result.SkippedDuplicates}, failed={result.Failed}");
    });
}, checkUsernameArg);
usersCommand.AddCommand(checkCommand);

rootCommand.AddCommand(usersCommand);

var checkAllCommand = new Command("check-all", "Run one full check cycle for all due users.");
checkAllCommand.SetHandler(async () =>
{
    Environment.ExitCode = await RunWithHostAsync(async sp =>
    {
        var orchestrator = sp.GetRequiredService<IStoryCheckOrchestrator>();
        var result = await orchestrator.RunDueUsersAsync(CancellationToken.None);
        Console.WriteLine(
            $"Cycle complete. users={result.UsersChecked}, downloaded={result.StoriesDownloaded}, skipped={result.StoriesSkipped}, sessionStopped={result.StoppedDueToSessionIssue}");
        if (result.StoppedDueToSessionIssue)
        {
            Environment.ExitCode = 2;
        }
    });
});
rootCommand.AddCommand(checkAllCommand);

if (args.Length > 0
    && !args[0].Equals("login", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("users", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("check-all", StringComparison.OrdinalIgnoreCase)
    && !IsKnownCliCommand(args))
{
    // Allow design-time host tools (dotnet ef) to build the host without CLI interception.
    return await RunWorkerAsync(args);
}

if (args.Length > 0 && IsKnownCliCommand(args))
{
    return await rootCommand.InvokeAsync(args);
}

return await RunWorkerAsync(args);

static bool IsKnownCliCommand(string[] args)
{
    if (args.Length == 0)
    {
        return false;
    }

    return args[0] is "login" or "users" or "check-all";
}

static async Task<int> RunWorkerAsync(string[] args)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    try
    {
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureSerilog(builder);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<StoryArchiverWorker>();

        using var host = builder.Build();
        await host.Services.EnsureDatabaseMigratedAsync(CancellationToken.None);
        await host.RunAsync();
        return Environment.ExitCode;
    }
    catch (HostAbortedException)
    {
        throw;
    }
    catch (InstagramSessionExpiredException ex)
    {
        Log.Fatal(ex, "Instagram session expired. Manual login required. Exiting with code 2 to avoid Docker restart loops.");
        return 2;
    }
    catch (InstagramChallengeDetectedException ex)
    {
        Log.Fatal(ex, "Instagram challenge detected. Manual intervention required. Exiting with code 2.");
        return 2;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Worker terminated unexpectedly.");
        return 1;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

static async Task<int> RunWithHostAsync(Func<IServiceProvider, Task> action)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    try
    {
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        ConfigureSerilog(builder);
        builder.Services.AddInfrastructure(builder.Configuration);
        using var host = builder.Build();
        await host.Services.EnsureDatabaseMigratedAsync(CancellationToken.None);
        using var scope = host.Services.CreateScope();
        await action(scope.ServiceProvider);
        return Environment.ExitCode == 0 ? 0 : Environment.ExitCode;
    }
    catch (Exception ex) when (ex is InstagramSessionExpiredException or InstagramChallengeDetectedException)
    {
        Log.Fatal(ex, "Session/challenge issue. Manual login required.");
        return 2;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Command failed.");
        return 1;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

static async Task<int> RunLoginAsync()
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    try
    {
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        ConfigureSerilog(builder);
        builder.Configuration["Instagram:Headless"] = "false";
        builder.Services.AddInfrastructure(builder.Configuration);
        using var host = builder.Build();
        await host.Services.EnsureDatabaseMigratedAsync(CancellationToken.None);

        var options = host.Services.GetRequiredService<IOptions<InstagramOptions>>().Value;

        // Login always uses a fresh headful context without requiring an existing storage state.
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(options.BaseUrl.TrimEnd('/') + "/accounts/login/");

        Console.WriteLine();
        Console.WriteLine("Browser opened. Log in to Instagram manually (including 2FA/checkpoint if prompted).");
        Console.WriteLine("When the home feed is visible and you are fully authenticated, return here and press ENTER.");
        Console.WriteLine();
        Console.ReadLine();

        var storagePath = Path.GetFullPath(options.StorageStatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = storagePath });

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(storagePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Console.WriteLine($"Storage state saved to {storagePath}");
        Console.WriteLine("You can now run the worker or CLI check commands.");
        return 0;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Login failed.");
        return 1;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

static async Task SetUserActiveAsync(IServiceProvider sp, string username, bool isActive)
{
    var normalized = UsernameNormalizer.Normalize(username);
    var repo = sp.GetRequiredService<IMonitoredUserRepository>();
    var clock = sp.GetRequiredService<IAppClock>();
    var user = await repo.GetByUsernameAsync(normalized, CancellationToken.None)
        ?? throw new InvalidOperationException($"User '{normalized}' not found.");
    user.IsActive = isActive;
    user.UpdatedAt = clock.UtcNow;
    if (isActive)
    {
        user.NextCheckAt ??= clock.UtcNow;
    }

    await repo.UpdateAsync(user, CancellationToken.None);
    await repo.SaveChangesAsync(CancellationToken.None);
}

static void ConfigureSerilog(HostApplicationBuilder builder)
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/instagram-story-archiver-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true)
        .CreateLogger();

    builder.Services.AddSerilog();
}
