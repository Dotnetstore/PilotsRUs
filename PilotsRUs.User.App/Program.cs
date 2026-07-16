using Avalonia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Infrastructure;
using PilotsRUs.User.App.Services;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var hostBuilder = Host.CreateApplicationBuilder(args);

        hostBuilder.Services
            .AddOptions<ApiOptions>()
            .Bind(hostBuilder.Configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // IDbContextFactory<T>, not AddDbContext - matches API.WebApi's convention for non-Identity
        // DbContexts. UserAppDbContext has no entities yet (see CLAUDE.md's "User.App architecture"
        // section), but the registration/migration pipeline is set up now for future features that do.
        hostBuilder.Services.AddDbContextFactory<UserAppDbContext>(options =>
            options.UseSqlite($"Data Source={UserAppDbContext.GetDefaultDbPath()}"));

        hostBuilder.Services.AddSingleton<IAuthSessionService, AuthSessionService>();
        hostBuilder.Services.AddTransient<IAccountApiClient, AccountApiClient>();
        hostBuilder.Services.AddTransient<IScheduleApiClient, ScheduleApiClient>();
        hostBuilder.Services.AddTransient<IFlightAssignmentService, FlightAssignmentService>();
        hostBuilder.Services.AddTransient<BearerTokenHandler>();

        hostBuilder.Services.AddHttpClient("Api", (services, client) =>
        {
            var apiOptions = services.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = new Uri(apiOptions.BaseAddress);
        }).AddHttpMessageHandler<BearerTokenHandler>();

        hostBuilder.Services.AddTransient<MainWindowViewModel>();

        var host = hostBuilder.Build();

        // Migrated unconditionally, no IsDevelopment()-style gate - a desktop app has no such distinction.
        var dbContextFactory = host.Services.GetRequiredService<IDbContextFactory<UserAppDbContext>>();
        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Database.Migrate();
        }

        App.Services = host.Services;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
