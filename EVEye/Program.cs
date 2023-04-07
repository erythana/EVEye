﻿using Avalonia;
using Avalonia.ReactiveUI;
using System;
using EVEye.DependencyInjection;
using EVEye.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EVEye;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var services = BuildServices();
        var startupLogger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            startupLogger.LogCritical(e, $"Unhandled exception in {ApplicationConstants.ApplicationName} - Application shutdown.");
        }

    }

    private static ServiceProvider BuildServices()
    {
        return ContainerConfiguration
            .AddLogging(new ServiceCollection()
            .Configure())
            .BuildServiceProvider();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => new App(BuildServices()))
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}