using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CrewChiefV4.GameState;

namespace CrewChiefV4.Events.PubSub
{
    public class GameEventDebugForm : Form
    {
        private readonly Timer resetTimer;
        private readonly ToolTip subscriberToolTip;
        private readonly Label statusLabel;
        private readonly RadioButton[] eventButtons;
        private RadioButton selectedButton;
        private GameStateData currentGameState;

        public GameEventDebugForm()
        {
            Text = "Game Event Debug";
            StartPosition = FormStartPosition.CenterParent;

            GameEvent[] gameEvents = Enum.GetValues(typeof(GameEvent))
                .Cast<GameEvent>()
                .OrderBy(gameEvent => gameEvent.ToString())
                .ToArray();

            // Fill rows from left to right, with no more than ten rows.
            int columns = Math.Max(1, (gameEvents.Length + 9) / 10);
            int rows = (gameEvents.Length + columns - 1) / columns;
            int longestLabel = gameEvents.Max(gameEvent =>
                TextRenderer.MeasureText(gameEvent.ToString(), Font).Width);
            int columnWidth = longestLabel + 55;
            int rowHeight = Math.Max(36, Font.Height + 16);
            int contentWidth = columns * columnWidth + 24;
            int contentHeight = rows * rowHeight + 24;
            int headerHeight = Math.Max(48, Font.Height * 3);
            int statusHeight = Math.Max(28, Font.Height + 12);

            ClientSize = new Size(contentWidth, headerHeight + contentHeight + statusHeight);
            MinimumSize = Size;

            Label rubric = new Label();
            rubric.Text = "These events can be generated while Crew Chief is running. " +
                "Select an event to publish it to its subscribers.";
            rubric.Dock = DockStyle.Top;
            rubric.Height = headerHeight;
            rubric.Padding = new Padding(12, 10, 12, 4);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Height = statusHeight;
            statusLabel.Padding = new Padding(12, 4, 12, 4);
            statusLabel.ForeColor = Color.DarkRed;
            statusLabel.Text = "No game state available - start Crew Chief to publish events.";

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(12);
            panel.ColumnCount = columns;
            panel.RowCount = rows;
            panel.AutoScroll = true;
            panel.MouseEnter += EventArea_MouseEnter;

            for (int column = 0; column < columns; column++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }

            for (int row = 0; row < rows; row++)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            }

            subscriberToolTip = new ToolTip();
            subscriberToolTip.AutoPopDelay = 15000;
            subscriberToolTip.InitialDelay = 250;
            subscriberToolTip.ReshowDelay = 100;

            eventButtons = new RadioButton[gameEvents.Length];
            for (int i = 0; i < gameEvents.Length; i++)
            {
                GameEvent gameEvent = gameEvents[i];
                RadioButton button = new RadioButton();
                button.Text = gameEvent.ToString();
                button.Tag = gameEvent;
                button.Dock = DockStyle.Fill;
                button.AutoEllipsis = false;
                button.AutoSize = false;
                button.CheckedChanged += EventButton_CheckedChanged;
                button.MouseEnter += EventButton_MouseEnter;

                eventButtons[i] = button;
                panel.Controls.Add(button, i / rows, i % rows);
            }

            Controls.Add(panel);
            Controls.Add(statusLabel);
            Controls.Add(rubric);

            resetTimer = new Timer();
            resetTimer.Interval = 1000;
            resetTimer.Tick += ResetTimer_Tick;

            RefreshSubscriptions();
        }

        public void UpdateGameState(GameStateData gameState)
        {
            currentGameState = gameState;
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke(new Action(UpdateStatus));
            }
            else
            {
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            if (IsDisposed)
            {
                return;
            }

            statusLabel.Text = currentGameState == null
                ? "No game state available - start Crew Chief to publish events."
                : "Ready - hover over an event to see its subscribers.";
            statusLabel.ForeColor = currentGameState == null
                ? Color.DarkRed : SystemColors.ControlText;
        }

        private void EventArea_MouseEnter(object sender, EventArgs e)
        {
            RefreshSubscriptions();
        }

        private void RefreshSubscriptions()
        {
            foreach (RadioButton button in eventButtons)
            {
                GameEvent gameEvent = (GameEvent)button.Tag;
                string[] handlers = EventBus.GetSubscriberDescriptions(gameEvent);
                button.Enabled = handlers.Length > 0;
                subscriberToolTip.SetToolTip(button, handlers.Length == 0
                    ? "No subscribers"
                    : string.Join(Environment.NewLine, handlers));
            }
        }

        private void EventButton_MouseEnter(object sender, EventArgs e)
        {
            // Refresh when entering a button as well: moving between buttons
            // does not necessarily raise MouseEnter on the parent panel.
            RefreshSubscriptions();
        }

        private void EventButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton button = (RadioButton)sender;
            if (!button.Checked)
            {
                return;
            }

            resetTimer.Stop();
            selectedButton = button;

            if (currentGameState == null)
            {
                button.Checked = false;
                selectedButton = null;
                UpdateStatus();
                return;
            }

            EventBus.Publish((GameEvent)button.Tag, currentGameState);
            resetTimer.Start();
        }

        private void ResetTimer_Tick(object sender, EventArgs e)
        {
            resetTimer.Stop();
            if (selectedButton != null)
            {
                selectedButton.Checked = false;
                selectedButton = null;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            resetTimer.Stop();
            resetTimer.Dispose();
            subscriberToolTip.Dispose();
            base.OnFormClosed(e);
        }
    }
}
