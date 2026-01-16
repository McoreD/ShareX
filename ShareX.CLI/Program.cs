#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.CommandLine;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Newtonsoft.Json;
using ShareX.ScreenCaptureLib;

namespace ShareX.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("ShareX CLI - Command line interface for ShareX capture operations");

        var captureCommand = new Command("capture", "Capture a rectangle region of the screen");

        var outputDirOption = new Option<string>(
            "--outputDir",
            description: "Output directory for capture results",
            getDefaultValue: () => Directory.GetCurrentDirectory());

        var widthOption = new Option<int>(
            "--width",
            description: "Width of the capture rectangle",
            getDefaultValue: () => 500);

        var heightOption = new Option<int>(
            "--height",
            description: "Height of the capture rectangle",
            getDefaultValue: () => 500);

        var seedOption = new Option<int?>(
            "--seed",
            description: "Random seed for reproducible rectangle placement (uses current time if not specified)");

        var xOption = new Option<int?>(
            "--x",
            description: "Explicit X coordinate (overrides random placement)");

        var yOption = new Option<int?>(
            "--y",
            description: "Explicit Y coordinate (overrides random placement)");

        var logOption = new Option<bool>(
            "--log",
            description: "Enable detailed logging",
            getDefaultValue: () => true);

        captureCommand.AddOption(outputDirOption);
        captureCommand.AddOption(widthOption);
        captureCommand.AddOption(heightOption);
        captureCommand.AddOption(seedOption);
        captureCommand.AddOption(xOption);
        captureCommand.AddOption(yOption);
        captureCommand.AddOption(logOption);

        captureCommand.SetHandler(async (outputDir, width, height, seed, x, y, log) =>
        {
            await ExecuteCaptureAsync(outputDir, width, height, seed, x, y, log);
        }, outputDirOption, widthOption, heightOption, seedOption, xOption, yOption, logOption);

        rootCommand.AddCommand(captureCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task ExecuteCaptureAsync(string outputDir, int width, int height, int? seed, int? x, int? y, bool log)
    {
        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputDir);

            // Get virtual screen bounds
            var virtualBounds = SystemInformation.VirtualScreen;

            if (log)
            {
                Console.WriteLine($"Virtual Screen Bounds: X={virtualBounds.X}, Y={virtualBounds.Y}, Width={virtualBounds.Width}, Height={virtualBounds.Height}");
            }

            // Determine rectangle placement
            int rectX, rectY;
            int actualSeed;

            if (x.HasValue && y.HasValue)
            {
                // Use explicit coordinates
                rectX = x.Value;
                rectY = y.Value;
                actualSeed = seed ?? 0;
                if (log) Console.WriteLine($"Using explicit coordinates: X={rectX}, Y={rectY}");
            }
            else
            {
                // Generate random placement within virtual screen
                actualSeed = seed ?? Environment.TickCount;
                var random = new Random(actualSeed);

                // Calculate valid range for random placement
                int minX = virtualBounds.X;
                int maxX = virtualBounds.Right - width;
                int minY = virtualBounds.Y;
                int maxY = virtualBounds.Bottom - height;

                if (maxX < minX || maxY < minY)
                {
                    Console.WriteLine("ERROR: Capture rectangle size exceeds virtual screen bounds");
                    return;
                }

                rectX = random.Next(minX, maxX + 1);
                rectY = random.Next(minY, maxY + 1);

                if (log) Console.WriteLine($"Random seed: {actualSeed}, Generated coordinates: X={rectX}, Y={rectY}");
            }

            var captureRect = new Rectangle(rectX, rectY, width, height);

            if (log)
            {
                Console.WriteLine($"Capture Rectangle: X={captureRect.X}, Y={captureRect.Y}, Width={captureRect.Width}, Height={captureRect.Height}");
            }

            // Perform capture using ShareX Screenshot class
            var screenshot = new Screenshot
            {
                CaptureCursor = false,
                CaptureClientArea = false,
                RemoveOutsideScreenArea = false, // We want exact coordinates
                CaptureShadow = false,
                AutoHideTaskbar = false
            };

            if (log) Console.WriteLine("Capturing rectangle...");

            Bitmap? capturedBitmap = null;
            await Task.Run(() =>
            {
                capturedBitmap = screenshot.CaptureRectangle(captureRect);
            });

            if (capturedBitmap == null)
            {
                Console.WriteLine("ERROR: Capture failed - null bitmap returned");
                return;
            }

            if (log)
            {
                Console.WriteLine($"Captured bitmap size: {capturedBitmap.Width}x{capturedBitmap.Height}");
            }

            // Generate output filenames
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var captureFilename = $"sharex_capture_{timestamp}.png";
            var metadataFilename = $"sharex_capture_{timestamp}.json";
            var capturePath = Path.Combine(outputDir, captureFilename);
            var metadataPath = Path.Combine(outputDir, metadataFilename);

            // Save the capture
            capturedBitmap.Save(capturePath, ImageFormat.Png);
            if (log) Console.WriteLine($"Saved capture to: {capturePath}");

            // Create and save metadata
            var metadata = new CaptureMetadata
            {
                Timestamp = DateTime.Now,
                Seed = actualSeed,
                Rectangle = new RectangleData
                {
                    X = captureRect.X,
                    Y = captureRect.Y,
                    Width = captureRect.Width,
                    Height = captureRect.Height
                },
                VirtualScreenBounds = new RectangleData
                {
                    X = virtualBounds.X,
                    Y = virtualBounds.Y,
                    Width = virtualBounds.Width,
                    Height = virtualBounds.Height
                },
                CaptureMethod = "ShareX.ScreenCaptureLib.Screenshot.CaptureRectangle (GDI BitBlt)",
                CapturePath = capturePath,
                ActualCaptureSize = new SizeData
                {
                    Width = capturedBitmap.Width,
                    Height = capturedBitmap.Height
                }
            };

            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            await File.WriteAllTextAsync(metadataPath, json);
            if (log) Console.WriteLine($"Saved metadata to: {metadataPath}");

            // Output summary for piping
            Console.WriteLine();
            Console.WriteLine("=== CAPTURE SUMMARY ===");
            Console.WriteLine($"CapturePath: {capturePath}");
            Console.WriteLine($"MetadataPath: {metadataPath}");
            Console.WriteLine($"Rectangle: {captureRect.X},{captureRect.Y},{captureRect.Width},{captureRect.Height}");
            Console.WriteLine($"Seed: {actualSeed}");

            capturedBitmap.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

public class CaptureMetadata
{
    public DateTime Timestamp { get; set; }
    public int Seed { get; set; }
    public RectangleData Rectangle { get; set; } = new();
    public RectangleData VirtualScreenBounds { get; set; } = new();
    public string CaptureMethod { get; set; } = "";
    public string CapturePath { get; set; } = "";
    public SizeData ActualCaptureSize { get; set; } = new();
}

public class RectangleData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class SizeData
{
    public int Width { get; set; }
    public int Height { get; set; }
}
