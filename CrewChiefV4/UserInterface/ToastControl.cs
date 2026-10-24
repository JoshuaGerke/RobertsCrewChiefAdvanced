using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class ToastControl : Control
{
    private readonly Timer displayTimer;
    private readonly Timer fadeTimer;
    private readonly Timer copyBadgeTimer;

    private readonly string _message;
    private readonly Color _baseColor;

    private int _alpha;
    private bool _paused;
    private bool _fading;

    private bool _copiedBadge;

    private Action _closedCallback;

    private const int ToastWidth = 400;
    private const int PaddingSize = 12;
    private const int CornerRadius = 12;

    public ToastControl(string message, Color baseColor)
    {
        _message = message;
        _baseColor = baseColor;

        _alpha = 150;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Font = new Font("Segoe UI", 10f);
        Cursor = Cursors.Hand;

        Size textSize = TextRenderer.MeasureText(
            message,
            Font,
            new Size(ToastWidth - (PaddingSize * 2), int.MaxValue),
            TextFormatFlags.WordBreak);

        Width = ToastWidth;
        Height = textSize.Height + (PaddingSize * 2);

        displayTimer = new Timer();
        displayTimer.Interval = 3000;
        displayTimer.Tick += DisplayTimer_Tick;

        fadeTimer = new Timer();
        fadeTimer.Interval = 30;
        fadeTimer.Tick += FadeTimer_Tick;

        copyBadgeTimer = new Timer();
        copyBadgeTimer.Interval = 1200;
        copyBadgeTimer.Tick += CopyBadgeTimer_Tick;

        MouseClick += ToastControl_MouseClick;
    }

    public void BeginLifetime(Action closedCallback)
    {
        _closedCallback = closedCallback;
        displayTimer.Start();
    }

    private void DisplayTimer_Tick(object sender, EventArgs e)
    {
        displayTimer.Stop();

        if (_paused)
            return;

        _fading = true;
        fadeTimer.Start();
    }

    private void FadeTimer_Tick(object sender, EventArgs e)
    {
        if (_paused)
            return;

        if (_alpha > 4)
        {
            _alpha -= 4;
            Invalidate();
        }
        else
        {
            fadeTimer.Stop();

            if (_closedCallback != null)
                _closedCallback();
        }
    }

    private void CopyBadgeTimer_Tick(object sender, EventArgs e)
    {
        copyBadgeTimer.Stop();
        _copiedBadge = false;
        Invalidate();
    }

    private void ToastControl_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            TogglePause();
        }
        else if (e.Button == MouseButtons.Right)
        {
            try
            {
                Clipboard.SetText(_message);
            }
            catch
            {
                return;
            }

            _copiedBadge = true;
            copyBadgeTimer.Stop();
            copyBadgeTimer.Start();

            Invalidate();
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;

        if (_paused)
        {
            displayTimer.Stop();
            fadeTimer.Stop();
        }
        else
        {
            if (_fading)
                fadeTimer.Start();
            else
                displayTimer.Start();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // ---------- Shadow ----------
        for (int i = 5; i >= 1; i--)
        {
            Rectangle shadowRect = new Rectangle(
                i,
                i,
                Width - i - 1,
                Height - i - 1);

            using (GraphicsPath shadowPath =
                CreateRoundedRectangle(shadowRect, CornerRadius))
            using (SolidBrush shadowBrush =
                new SolidBrush(Color.FromArgb(8, Color.Black)))
            {
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        // ---------- Body ----------
        Rectangle bodyRect = new Rectangle(
            0,
            0,
            Width - 1,
            Height - 1);

        using (GraphicsPath bodyPath =
            CreateRoundedRectangle(bodyRect, CornerRadius))
        using (SolidBrush bodyBrush =
            new SolidBrush(Color.FromArgb(_alpha, _baseColor)))
        using (Pen borderPen =
            new Pen(Color.FromArgb(Math.Min(_alpha, 160), Color.Black)))
        {
            g.FillPath(bodyBrush, bodyPath);
            g.DrawPath(borderPen, bodyPath);
        }

        // ---------- Accent strip ----------
        using (SolidBrush accentBrush =
            new SolidBrush(Color.FromArgb(_alpha, ControlPaint.Dark(_baseColor))))
        {
            g.FillRectangle(accentBrush, 0, 0, 6, Height);
        }

        // ---------- Message ----------
        Rectangle textRect = new Rectangle(
            PaddingSize + 45, // shifted right to make room for badge
            PaddingSize,
            Width - (PaddingSize * 2) - 45,
            Height - (PaddingSize * 2));

        TextRenderer.DrawText(
            g,
            _message,
            Font,
            textRect,
            Color.Black,
            TextFormatFlags.WordBreak | TextFormatFlags.Left);

        // ---------- Badge (LEFT SIDE) ----------
        string badgeText = null;

        if (_copiedBadge)
            badgeText = "COPIED";
        else if (_paused)
            badgeText = "PAUSED";

        if (badgeText != null)
        {
            Size badgeSize = TextRenderer.MeasureText(badgeText, Font);

            Rectangle badgeRect = new Rectangle(
                14,
                (Height - badgeSize.Height) / 2,
                badgeSize.Width + 10,
                badgeSize.Height);

            using (SolidBrush badgeBrush =
                new SolidBrush(Color.FromArgb(220, Color.White)))
            using (Pen badgeBorder = new Pen(Color.Black))
            {
                g.FillRectangle(badgeBrush, badgeRect);
                g.DrawRectangle(badgeBorder, badgeRect);
            }

            TextRenderer.DrawText(
                g,
                badgeText,
                Font,
                badgeRect,
                Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int diameter = radius * 2;

        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);

        path.CloseFigure();
        return path;
    }
}