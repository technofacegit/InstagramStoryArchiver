using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Services;
using InstagramStoryArchiver.Infrastructure.Locking;
using InstagramStoryArchiver.Infrastructure.Persistence;
using InstagramStoryArchiver.Infrastructure.Persistence.Repositories;
using InstagramStoryArchiver.Infrastructure.Playwright;
using InstagramStoryArchiver.Infrastructure.Storage;
using InstagramStoryArchiver.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InstagramStoryArchiver.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InstagramOptions>()
            .Bind(configuration.GetSection(InstagramOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o =>
            {
                o.Validate();
                return true;
            }, "Instagram options validation failed.")
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/instagram-story-archiver.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IInstagramBrowserService, InstagramBrowserService>();
        services.AddSingleton<IInstanceLock, FileInstanceLock>();

        services.AddScoped<IMonitoredUserRepository, MonitoredUserRepository>();
        services.AddScoped<IArchivedStoryRepository, ArchivedStoryRepository>();
        services.AddScoped<IInstagramSessionService, InstagramSessionService>();
        services.AddScoped<IInstagramStoryResponseParser, InstagramStoryResponseParser>();
        services.AddScoped<IInstagramStoryDetector, InstagramStoryDetector>();
        services.AddScoped<IArchiveStorageService, ArchiveStorageService>();
        services.AddScoped<IStoryDownloadService, StoryDownloadService>();
        services.AddScoped<IStoryCheckOrchestrator, StoryCheckOrchestrator>();

        return services;
    }

    public static async Task EnsureDatabaseMigratedAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var instagram = scope.ServiceProvider.GetRequiredService<IOptions<InstagramOptions>>().Value;

        Directory.CreateDirectory(Path.GetFullPath(storage.ArchiveRootPath));
        Directory.CreateDirectory(Path.GetFullPath(storage.TemporaryRootPath));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(instagram.StorageStatePath))!);
        Directory.CreateDirectory("logs");
        Directory.CreateDirectory("data");

        await db.Database.MigrateAsync(cancellationToken);
    }
}
