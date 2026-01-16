using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ShareX.Editor;
using ShareX.Editor.Views;
using ShareX.HelpersLib;

namespace ShareX.EditorInterop
{
    /// <summary>
    /// Singleton host for the Avalonia application. Initializes Avalonia once and reuses the dispatcher.
    /// </summary>
    public static class AvaloniaAppHost
    {
        private static Thread? _avaloniaThread;
        private static ClassicDesktopStyleApplicationLifetime? _lifetime;
        private static ManualResetEventSlim _initEvent = new ManualResetEventSlim(false);
        private static bool _isInitialized;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes Avalonia on a dedicated STA thread. Call this once after WinForms main form is loaded.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized)
                    return;

                _avaloniaThread = new Thread(RunAvaloniaApp)
                {
                    Name = "AvaloniaUIThread",
                    IsBackground = true
                };
                _avaloniaThread.SetApartmentState(ApartmentState.STA);
                _avaloniaThread.Start();

                // Wait for Avalonia to initialize (with timeout)
                if (!_initEvent.Wait(TimeSpan.FromSeconds(10)))
                {
                    DebugHelper.WriteLine("AvaloniaAppHost: Initialization timed out.");
                }

                _isInitialized = true;
            }
        }

        private static void RunAvaloniaApp()
        {
            try
            {
                var builder = AppBuilder.Configure<ShareX.Editor.App>()
                    .UsePlatformDetect()
                    .LogToTrace();

                _lifetime = new ClassicDesktopStyleApplicationLifetime
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                builder.SetupWithLifetime(_lifetime);
                _initEvent.Set();

                // Run the dispatcher loop - this blocks until shutdown
                _lifetime.Start(Array.Empty<string>());
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "AvaloniaAppHost.RunAvaloniaApp failed.");
            }
        }

        /// <summary>
        /// Opens the EditorWindow with the specified image file.
        /// </summary>
        public static void OpenEditor(string? filePath)
        {
            if (!_isInitialized || _lifetime == null)
            {
                DebugHelper.WriteLine("AvaloniaAppHost: Not initialized. Call Initialize() first.");
                return;
            }

            // Post to Avalonia dispatcher to create and show window
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var window = new EditorWindow();

                    if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                    {
                        window.LoadImage(filePath);
                    }

                    window.Show();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "AvaloniaAppHost.OpenEditor failed.");
                }
            });
        }

        /// <summary>
        /// Shuts down the Avalonia application. Call this when ShareX is closing.
        /// </summary>
        public static void Shutdown()
        {
            if (_lifetime != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _lifetime.Shutdown();
                });
            }
        }
    }
}
