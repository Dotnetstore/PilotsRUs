using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.User.App.ViewModels;
using PilotsRUs.User.App.Views;

namespace PilotsRUs.User.App;

public partial class App : Application
{
    // Set by Program.Main before Avalonia starts - Avalonia constructs App itself (no constructor
    // injection hook), so this is the standard way to hand the DI container to it.
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
