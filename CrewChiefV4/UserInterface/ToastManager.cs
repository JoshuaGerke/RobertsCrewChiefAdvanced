using CrewChiefV4;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public static class ToastManager
{
    private static readonly bool useToasts = UserSettings.GetUserSettings().getBoolean("use_toast_notifications");
    private static readonly List<ToastControl> ActiveToasts = new List<ToastControl>();

    private const int ToastWidth = 400;
    private const int Margin = 10;

    public static void Exception(Control parent, string message)
    {
        AddToast(parent, message, Color.IndianRed);
    }

    public static void Error(Control parent, string message)
    {
        AddToast(parent, message, Color.Gold);
    }

    public static void Warning(Control parent, string message)
    {
        AddToast(parent, message, Color.LightSkyBlue);
    }

    public static void Success(Control parent, string message)
    {
        AddToast(parent, message, Color.LightGreen);
    }

    private static void AddToast(
        Control parent,
        string message,
        Color color)
    {
        if (!useToasts || parent == null || parent.IsDisposed)
        {
            return;
        }
        if (parent.InvokeRequired)
        {
            parent.BeginInvoke(new Action(() =>
                AddToast(parent, message, color)));

            return;
        }

        var toast = new ToastControl(
            message,
            color);

        int y = parent.ClientSize.Height - Margin;

        foreach (var existing in ActiveToasts)
            y -= existing.Height + Margin;

        y -= toast.Height;

        toast.Location = new Point(
            parent.ClientSize.Width - ToastWidth - Margin,
            y);

        parent.Controls.Add(toast);
        toast.BringToFront();

        ActiveToasts.Add(toast);

        // Only the oldest toast gets a timer
        if (ActiveToasts.Count == 1)
            StartLifetime(parent, toast);
    }

    private static void StartLifetime(
        Control parent,
        ToastControl toast)
    {
        toast.BeginLifetime(() =>
        {
            RemoveToast(parent, toast);
        });
    }

    private static void RemoveToast(
        Control parent,
        ToastControl toast)
    {
        ActiveToasts.Remove(toast);

        parent.Controls.Remove(toast);
        toast.Dispose();

        AnimateRelayout(parent);

        if (ActiveToasts.Count > 0)
            StartLifetime(parent, ActiveToasts[0]);
    }

    private static void AnimateRelayout(Control parent)
    {
        var targets = new Dictionary<ToastControl, int>();

        int y = parent.ClientSize.Height - Margin;

        foreach (var toast in ActiveToasts)
        {
            y -= toast.Height;
            targets.Add(toast, y);
            y -= Margin;
        }

        var animationTimer = new Timer
        {
            Interval = 15
        };

        animationTimer.Tick += (s, e) =>
        {
            bool finished = true;

            foreach (var pair in targets)
            {
                ToastControl toast = pair.Key;
                int targetY = pair.Value;

                if (toast.Top < targetY)
                {
                    toast.Top = Math.Min(
                        targetY,
                        toast.Top + 8);

                    finished = false;
                }
                else if (toast.Top > targetY)
                {
                    toast.Top = Math.Max(
                        targetY,
                        toast.Top - 8);

                    finished = false;
                }
            }

            if (finished)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }
        };

        animationTimer.Start();
    }

    public static void Reposition(Control parent)
    {
        int y = parent.ClientSize.Height - Margin;

        foreach (var toast in ActiveToasts)
        {
            y -= toast.Height;

            toast.Location = new Point(
                parent.ClientSize.Width - ToastWidth - Margin,
                y);

            y -= Margin;
        }
    }
}