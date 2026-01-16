using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        /// Requests an edit operation, returning the result as a byte array (PNG encoded).
        /// This is used for bridging synchronous/modal workflow tasks from WinForms.
        /// </summary>
        public static Task<byte[]?> RequestEditAsync(byte[] imageBytes)
        {
            if (!_isInitialized || _lifetime == null)
            {
                return Task.FromResult<byte[]?>(null);
            }

            var tcs = new TaskCompletionSource<byte[]?>();

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var window = new EditorWindow();
                    
                    // Load image from bytes
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        window.LoadImage(ms);
                    }

                    // Handle window closing to capture result
                    // For now, we assume if the window is closed, we take the result.
                    // Ideally we'd have an "OK" vs "Cancel" dialog result pattern.
                    window.Closed += (s, e) =>
                    {
                        try
                        {
                            var result = window.GetResultBytes();
                            tcs.TrySetResult(result);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    };

                    window.Show();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
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
