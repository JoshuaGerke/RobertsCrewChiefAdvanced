using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrewChiefV4
{
    // MessageBox-like form with a Copy button.
    // - Converts escaped sequences like "\n", "\r\n", "\t", "\uXXXX" into real characters.
    // - Ensures the message text is not highlighted when shown.
    public class MessageBoxWithCopy : Form
    {
        private TextBox textBox;
        private Button okButton;
        private Button copyButton;
        private TableLayoutPanel layout;
        private readonly string requestDisplay;
        private readonly string copyText;
        private readonly string combinedDisplay;

        private MessageBoxWithCopy(string request, string textToBeCopied, string caption, MessageBoxButtons buttons)
        {
            requestDisplay = ConvertEscapedSequences(request ?? string.Empty);
            copyText = ConvertEscapedSequences(textToBeCopied ?? string.Empty);
            // Show the request first, then the textToBeCopied. Separate with a blank line if both present.
            if (!string.IsNullOrEmpty(requestDisplay) && !string.IsNullOrEmpty(copyText))
                combinedDisplay = requestDisplay + Environment.NewLine + Environment.NewLine + copyText;
            else
                combinedDisplay = requestDisplay + copyText; // one of them may be empty

            Text = caption ?? string.Empty;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = true;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            // Initial size will be calculated onShown to match MessageBox-like sizing and to allow centering on the owner window.
            MinimumSize = new Size(320, 120);

            layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

                textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.None,
                Dock = DockStyle.Fill,
                WordWrap = true,
                Font = SystemFonts.MessageBoxFont,
                Text = combinedDisplay,
                TabStop = false,
                HideSelection = true,
                BackColor = SystemColors.Window
            };
            layout.Controls.Add(textBox, 0, 0);

            var buttonsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false
            };

            okButton = new Button { Text = "OK", AutoSize = true };
            okButton.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            copyButton = new Button { Text = "Copy", AutoSize = true };
            copyButton.Click += async (s, e) =>
            {
                try
                {
                    // Only the provided textToBeCopied should be copied to the clipboard.
                    Clipboard.SetText(copyText);
                    var original = copyButton.Text;
                    copyButton.Text = "Copied";
                    copyButton.Enabled = false;
                    await Task.Delay(900);
                    copyButton.Text = original;
                    copyButton.Enabled = true;
                }
                catch
                {
                    // ignore clipboard exceptions
                }
            };

            buttonsPanel.Controls.Add(okButton);
            buttonsPanel.Controls.Add(copyButton);

            layout.Controls.Add(buttonsPanel, 0, 2);

            AcceptButton = okButton;

            // Ensure the textbox is not focused or highlighted when the dialog appears.
            Shown += (s, e) =>
            {
                // Size the dialog similar to MessageBox: measure the text and pick a reasonable width/height.
                try
                {
                    // Determine maximum width: prefer the owner's client width if available, otherwise screen working area.
                    int screenMax = Screen.FromControl(this).WorkingArea.Width;
                    int ownerMax = (Owner != null) ? Math.Max(Owner.ClientSize.Width - 80, 400) : screenMax - 80;
                    int maxWidth = Math.Min(800, Math.Max(400, ownerMax));

                    // Measure the text using TextRenderer with word wrap
                    var textFlags = System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.WordBreak;
                    var measured = TextRenderer.MeasureText(combinedDisplay, textBox.Font, new Size(maxWidth, int.MaxValue), textFlags);

                    // Calculate dialog chrome and padding
                    int horizontalPadding = 48; // left+right padding
                    int verticalPadding = 40; // top padding + between text and buttons
                    int buttonsAreaHeight = okButton.PreferredSize.Height + 20;

                    int dialogClientWidth = Math.Max(MinimumSize.Width, Math.Min(maxWidth, measured.Width + horizontalPadding));
                    int dialogClientHeight = Math.Max(MinimumSize.Height, measured.Height + buttonsAreaHeight + verticalPadding);

                    // Set client size directly so the textbox gets the expected layout and wraps without scrollbars.
                    this.ClientSize = new Size(dialogClientWidth, dialogClientHeight);

                    // Center on owner if present
                    if (Owner != null)
                    {
                        var ownerRect = Owner is Form oform ? oform.Bounds : new Rectangle(Owner.Location, Owner.Size);
                        this.StartPosition = FormStartPosition.Manual;
                        this.Location = new Point(ownerRect.Left + (ownerRect.Width - this.Width) / 2,
                                                  ownerRect.Top + (ownerRect.Height - this.Height) / 2);
                    }
                }
                catch
                {
                    // ignore sizing failures and fall back to default layout
                }

                ActiveControl = okButton;
                textBox.SelectionStart = 0;
                textBox.SelectionLength = 0;
            };
        }

        private static string ConvertEscapedSequences(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            try
            {
                // Convert escaped sequences like "\n", "\r\n", "\t", "\uXXXX", etc.
                string un = System.Text.RegularExpressions.Regex.Unescape(s);
                // Normalize line endings: CRLF -> LF, CR -> LF, then LF -> Environment.NewLine
                // This ensures consistent CRLF display in the textbox on Windows.
                un = un.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                return un;
            }
            catch
            {
                // Fallback: handle common escaped newline/tab sequences
                return s.Replace("\\r\\n", Environment.NewLine)
                        .Replace("\\n", Environment.NewLine)
                        .Replace("\\r", Environment.NewLine)
                        .Replace("\\t", "\t");
            }
        }

        public static DialogResult Show(IWin32Window owner, string request, string textToBeCopied, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            using (var dlg = new MessageBoxWithCopy(request, textToBeCopied, caption, buttons))
            {
                return dlg.ShowDialog(owner);
            }
        }

        public static DialogResult Show(string request, string textToBeCopied, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            return Show(null, request, textToBeCopied, caption, buttons);
        }
    }
}