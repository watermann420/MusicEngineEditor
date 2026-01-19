using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Create and show main window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
        services.AddSingleton<EngineService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ProjectExplorerViewModel>();
        services.AddTransient<OutputViewModel>();
        services.AddTransient<EditorTabViewModel>();
    }
}
