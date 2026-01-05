using ShareX.Editor;
using ShareX.Editor.Annotations;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShareX.EditorInterop
{
    /// <summary>
    /// WinForms host for ShareX.Editor. Provides a simple toolbar and SKIA canvas to annotate an image.
    /// </summary>
    internal sealed class WinFormsEditorForm : Form
    {
        private readonly EditorCore _editor = new();
        private readonly SKControl _canvas;
        private readonly Panel _scrollPanel;
        private readonly ToolStripComboBox _toolSelector;
        private readonly ToolStripComboBox _strokeSelector;
        private readonly ToolStripButton _colorButton;
        private readonly ToolStripButton _undoButton;
        private readonly ToolStripButton _redoButton;
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripStatusLabel _statusLabel;

        internal WinFormsEditorForm(SKBitmap bitmap, string? titleHint = null)
        {
            Text = string.IsNullOrWhiteSpace(titleHint) ? "ShareX Editor" : $"ShareX Editor - {titleHint}";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 700);
            KeyPreview = true;

            _editor.InvalidateRequested += OnInvalidateRequested;
            _editor.StatusTextChanged += OnStatusTextChanged;
            _editor.LoadImage(bitmap.Copy());

            _canvas = new SKControl
            {
                Dock = DockStyle.None,
                Location = new Point(0, 0),
                Size = new Size(_editor.SourceImage?.Width ?? 0, _editor.SourceImage?.Height ?? 0),
                BackColor = Color.White
            };
            _canvas.PaintSurface += CanvasOnPaintSurface;
            _canvas.MouseDown += CanvasOnMouseDown;
            _canvas.MouseMove += CanvasOnMouseMove;
            _canvas.MouseUp += CanvasOnMouseUp;

            _scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(28, 28, 28)
            };
            _scrollPanel.Controls.Add(_canvas);
            _scrollPanel.AutoScrollMinSize = _canvas.Size;

            _toolSelector = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };
            foreach (EditorTool tool in Enum.GetValues(typeof(EditorTool)))
            {
                _toolSelector.Items.Add(tool);
            }
            _toolSelector.SelectedItem = _editor.ActiveTool;
            _toolSelector.SelectedIndexChanged += (_, _) =>
            {
                if (_toolSelector.SelectedItem is EditorTool tool)
                {
                    _editor.ActiveTool = tool;
                }
            };

            _strokeSelector = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 60
            };
            foreach (float width in new float[] { 1, 2, 3, 4, 5, 6, 8, 10, 12, 16 })
            {
                _strokeSelector.Items.Add(width);
            }
            _strokeSelector.SelectedItem = _editor.StrokeWidth;
            _strokeSelector.SelectedIndexChanged += (_, _) =>
            {
                if (_strokeSelector.SelectedItem is float width)
                {
                    _editor.StrokeWidth = width;
                }
            };

            _colorButton = new ToolStripButton("Color")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _colorButton.Click += (_, _) => ChooseColor();
            UpdateColorButton();

            _undoButton = new ToolStripButton("Undo", null, (_, _) => { _editor.Undo(); _canvas.Invalidate(); })
            {
                Enabled = false
            };
            _redoButton = new ToolStripButton("Redo", null, (_, _) => { _editor.Redo(); _canvas.Invalidate(); })
            {
                Enabled = false
            };

            ToolStrip topBar = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Stretch = true
            };
            topBar.Items.Add(new ToolStripLabel("Tool"));
            topBar.Items.Add(_toolSelector);
            topBar.Items.Add(new ToolStripSeparator());
            topBar.Items.Add(new ToolStripLabel("Stroke"));
            topBar.Items.Add(_strokeSelector);
            topBar.Items.Add(_colorButton);
            topBar.Items.Add(new ToolStripSeparator());
            topBar.Items.Add(_undoButton);
            topBar.Items.Add(_redoButton);

            Button okButton = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
            Button cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            FlowLayoutPanel buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(8),
                WrapContents = false
            };
            buttonsPanel.Controls.Add(okButton);
            buttonsPanel.Controls.Add(cancelButton);

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(8)
            };
            buttonsPanel.Dock = DockStyle.Right;
            buttonsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bottomPanel.Controls.Add(buttonsPanel);

            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Ready");
            _statusStrip.Items.Add(_statusLabel);

            Controls.Add(_scrollPanel);
            Controls.Add(topBar);
            Controls.Add(bottomPanel);
            Controls.Add(_statusStrip);

            topBar.Dock = DockStyle.Top;
            _statusStrip.Dock = DockStyle.Bottom;

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Resize += (_, _) => _canvas.Invalidate();
        }

        internal SKBitmap? GetResultSnapshot()
        {
            return _editor.GetSnapshot();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _editor.InvalidateRequested -= OnInvalidateRequested;
                _editor.StatusTextChanged -= OnStatusTextChanged;
            }

            base.Dispose(disposing);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                _editor.Undo();
                _canvas.Invalidate();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                _editor.Redo();
                _canvas.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                _editor.DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _editor.Deselect();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        private void CanvasOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            e.Surface.Canvas.Clear(SKColors.Transparent);
            _editor.Render(e.Surface.Canvas);
            UpdateUndoRedoButtons();
        }

        private void CanvasOnMouseDown(object? sender, MouseEventArgs e)
        {
            _canvas.Focus();
            _editor.OnPointerPressed(new SKPoint(e.X, e.Y), e.Button == MouseButtons.Right);
        }

        private void CanvasOnMouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None)
            {
                _editor.OnPointerMoved(new SKPoint(e.X, e.Y));
            }
        }

        private void CanvasOnMouseUp(object? sender, MouseEventArgs e)
        {
            _editor.OnPointerReleased(new SKPoint(e.X, e.Y));
        }

        private void OnInvalidateRequested()
        {
            if (IsHandleCreated && !IsDisposed)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _canvas.Invalidate();
                        UpdateUndoRedoButtons();
                    }));
                }
                else
                {
                    _canvas.Invalidate();
                    UpdateUndoRedoButtons();
                }
            }
        }

        private void OnStatusTextChanged(string text)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => _statusLabel.Text = text));
                }
                else
                {
                    _statusLabel.Text = text;
                }
            }
        }

        private void UpdateUndoRedoButtons()
        {
            _undoButton.Enabled = _editor.CanUndo;
            _redoButton.Enabled = _editor.CanRedo;
        }

        private void ChooseColor()
        {
            using ColorDialog dialog = new ColorDialog
            {
                Color = ColorTranslator.FromHtml(_editor.StrokeColor)
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _editor.StrokeColor = ColorTranslator.ToHtml(dialog.Color);
                UpdateColorButton();
            }
        }

        private void UpdateColorButton()
        {
            Color color = ColorTranslator.FromHtml(_editor.StrokeColor);
            _colorButton.BackColor = color;
            _colorButton.ForeColor = color.GetBrightness() < 0.5 ? Color.White : Color.Black;
        }
    }
}
