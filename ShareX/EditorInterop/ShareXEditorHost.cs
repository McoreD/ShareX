using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ShareX.HelpersLib;
using System.Windows.Forms;
using SkiaSharp;

namespace ShareX.EditorInterop
{
    internal static class ShareXEditorHost
    {
        internal static bool TryAnnotateWithShareXEditor(Bitmap source, string filePath, out Bitmap? edited)
        {
            edited = null;

            if (source == null)
            {
                return false;
            }

            try
            {
                using SKBitmap skBitmap = ConvertToSkia(source);

                using WinFormsEditorForm editorForm = new WinFormsEditorForm(skBitmap, Path.GetFileName(filePath));
                DialogResult result = editorForm.ShowDialog();

                if (result != DialogResult.OK)
                {
                    return false; // user canceled or host failed
                }

                using SKBitmap? snapshot = editorForm.GetResultSnapshot();
                edited = ConvertToGdi(snapshot);
                return edited != null;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "ShareX.Editor integration stub failed.");
                return false;
            }
        }

        private static SKBitmap ConvertToSkia(Bitmap source)
        {
            using MemoryStream stream = new MemoryStream();
            source.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }

        private static Bitmap? ConvertToGdi(SKBitmap? source)
        {
            try
            {
                if (source == null)
                {
                    return null;
                }

                using SKImage image = SKImage.FromBitmap(source);
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using MemoryStream ms = new MemoryStream(data.ToArray());
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to convert SKBitmap back to Bitmap.");
                return null;
            }
        }
    }
}
