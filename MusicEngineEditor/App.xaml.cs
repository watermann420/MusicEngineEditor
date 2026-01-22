using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Safe Mode flag - when true, audio engine initialization is skipped.
    /// Use --safe or --safe-mode command line argument to enable.
    /// </summary>
    public static bool SafeMode { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for safe mode command line argument
        SafeMode = e.Args.Any(arg =>
            arg.Equals("--safe", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--safe-mode", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("/safe", StringComparison.OrdinalIgnoreCase));

        if (SafeMode)
        {
            System.Diagnostics.Debug.WriteLine("Starting in SAFE MODE - Audio engine disabled");
        }

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Load settings and apply saved theme
        await ApplyStartupThemeAsync();

        // Create and show main window
        var mainWindow = new MainWindow();

        if (SafeMode)
        {
            mainWindow.Title += " [SAFE MODE - Audio Disabled]";
        }

        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISoundPackService, SoundPackService>();
        services.AddSingleton<EngineService>();

        // Playback services (singletons accessed via Instance property)
        services.AddSingleton(_ => PlaybackService.Instance);
        services.AddSingleton(_ => AudioEngineService.Instance);

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ProjectExplorerViewModel>();
        services.AddTransient<OutputViewModel>();
        services.AddTransient<EditorTabViewModel>();
        services.AddTransient<SampleBrowserViewModel>();
        services.AddTransient<TransportViewModel>();
    }

    /// <summary>
    /// Loads settings and applies the saved theme on startup
    /// </summary>
    private static async System.Threading.Tasks.Task ApplyStartupThemeAsync()
    {
        try
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            var themeService = Services.GetRequiredService<IThemeService>();

            // Load settings to get the saved theme
            var settings = await settingsService.LoadSettingsAsync();
            var savedTheme = settings.Editor.Theme;

            // Apply the saved theme (or default to Dark if not set)
            if (!string.IsNullOrWhiteSpace(savedTheme))
            {
                themeService.ApplyTheme(savedTheme);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply startup theme: {ex.Message}");
            // Fall back to default theme (Dark) which is already loaded in App.xaml
        }
    }
}
