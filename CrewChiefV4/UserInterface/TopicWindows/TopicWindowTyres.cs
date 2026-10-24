using CrewChiefV4.Events.PubSub;
using CrewChiefV4.GameState;

using System;
using System.Drawing;
using System.Timers; // Add at the top
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface.TopicWindows
{
    public partial class TopicWindowTyres : Form
    {
        private static TopicWindowTyres _instance;
        private static int flTyreTempCallCount = 0;
        private static System.Timers.Timer rateTimer;
        public static bool JustOpened { get; private set; }

        // cached numeric thresholds for status calculation
        private static float coldThresholdValue;
        private static float warmThresholdValue;
        private static float hotThresholdValue;
        private static float cookingThresholdValue;

        // previous statuses for logging status changes
        private static TempStatus flPrevStatus = TempStatus.Cold;
        private static TempStatus frPrevStatus = TempStatus.Cold;
        private static TempStatus rlPrevStatus = TempStatus.Cold;
        private static TempStatus rrPrevStatus = TempStatus.Cold;

        public TopicWindowTyres()
        {
            _instance = this;
            InitializeComponent();
            JustOpened = true;

            // Initialize and start the timer to update the rate every second
            rateTimer = new System.Timers.Timer(1000);
            rateTimer.Elapsed += RateTimer_Elapsed;
            rateTimer.AutoReset = true;
            rateTimer.Start();

            EventBus.Subscribe(GameEvent.SessionChanged, SessionConstants);
            EventBus.Subscribe(GameEvent.PlayerLeftPits, SessionConstants);
        }

        public static void SessionConstants(GameStateData currentGameState)
        {
            var tyreThresholds = CarData.getTyreTempThresholds(currentGameState.carClass);
            TopicWindowTyres.SetCarClass(currentGameState.carClass.carClassEnumString);
            TopicWindowTyres.SetTyreType(currentGameState.TyreData.FrontLeftTyreType.ToString());
            TopicWindowTyres.SetColdThreshold(tyreThresholds[0].upperThreshold);
            TopicWindowTyres.SetWarmThreshold(tyreThresholds[1].upperThreshold);
            TopicWindowTyres.SetHotThreshold(tyreThresholds[2].upperThreshold);
            TopicWindowTyres.SetCookingThreshold(tyreThresholds[3].upperThreshold);
        }
        private void RateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Capture and reset the count atomically
            int count = System.Threading.Interlocked.Exchange(ref flTyreTempCallCount, 0);
            double hz = count / 1.0; // Calls per second

            // Update the UI on the main thread
            if (_instance != null && !_instance.IsDisposed && _instance.Visible)
            {
                _instance.BeginInvoke((Action)(() =>
                {
                    _instance.textBoxRate.Text = hz.ToString("0.##") + " Hz";
                }));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _instance = null;
            JustOpened = false;
            EventBus.Unsubscribe(this);
            base.OnFormClosed(e);
        }

        private static void SetTextBoxFloat(TextBox textBox, float value)
        {
            if (_instance == null || _instance.IsDisposed || !_instance.Visible || textBox == null) return;
            textBox.Text = value.ToString("0") + "°C";
        }

        private static void SetTextBoxString(TextBox textBox, string text)
        {
            if (_instance == null || _instance.IsDisposed || !_instance.Visible || textBox == null) return;
            textBox.Text = text;
        }
        enum TempStatus
        {
            Cold,
            Warm,
            Hot,
            Cooking
        }
        private static void SetTextBoxStatus(TextBox textBox, TempStatus tempStatus)
        {
            if (_instance == null || _instance.IsDisposed || !_instance.Visible || textBox == null) return;

            Color color;
            switch (tempStatus)
            {
                case TempStatus.Cold:
                    color = Color.Blue;
                    break;
                case TempStatus.Warm:
                    color = Color.Green;
                    break;
                case TempStatus.Hot:
                    color = Color.Orange;
                    break;
                case TempStatus.Cooking:
                    color = Color.Red;
                    break;
                default:
                    color = Color.Black;
                    break;
            }

            textBox.BackColor = color;
            textBox.ForeColor = Color.White;
        }

        private static TempStatus GetTempStatus(float value)
        {
            // Thresholds are inclusive of their range start. Cooking is highest priority.
            if (value >= hotThresholdValue) return TempStatus.Cooking;
            if (value >= warmThresholdValue) return TempStatus.Hot;
            if (value >= coldThresholdValue) return TempStatus.Warm;
            return TempStatus.Cold;
        }

        public static void SetFLTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Tyres)))
            {
                return; // Neither Topic window nor Tyre logging are active
            }
            SetTextBoxFloat(_instance?.textBoxFLTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != flPrevStatus)
            {
                Log.Tyres($"FL tyre status changed from {flPrevStatus} to {newStatus} ({value:0}°C)");
                flPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxFLTemp, newStatus);
            flTyreTempCallCount++;
            JustOpened = false; // Reset the flag after the first call
        }

        public static void SetFRTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Tyres)))
            {
                return; // Neither Topic window nor Tyre logging are active
            }
            SetTextBoxFloat(_instance?.textBoxFRTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != frPrevStatus)
            {
                Log.Tyres($"FR tyre status changed from {frPrevStatus} to {newStatus} ({value:0}°C)");
                frPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxFRTemp, newStatus);
        }
        public static void SetRLTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Tyres)))
            {
                return; // Neither Topic window nor Tyre logging are active
            }
            SetTextBoxFloat(_instance?.textBoxRLTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != rlPrevStatus)
            {
                Log.Tyres($"RL tyre status changed from {rlPrevStatus} to {newStatus} ({value:0}°C)");
                rlPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxRLTemp, newStatus);
        }
        public static void SetRRTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Tyres)))
            {
                return; // Neither Topic window nor Tyre logging are active
            }
            SetTextBoxFloat(_instance?.textBoxRRTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != rrPrevStatus)
            {
                Log.Tyres($"RR tyre status changed from {rrPrevStatus} to {newStatus} ({value:0}°C)");
                rrPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxRRTemp, newStatus);
        }

        #region Only run on session start or when car class changes - not every update
        private static void SetCarClass(string value)
        {
            Log.Tyres($"Car class set to '{value}'");
            SetTextBoxString(_instance?.textBoxCarClass, value == String.Empty ? "Undefined" : value);
        }
        private static void SetTyreType(string value)
        {
            Log.Tyres($"Tyre type set to '{value}'");
            SetTextBoxString(_instance?.textBoxTyreType, value);
        }
        private static void SetColdThreshold(float value)
        {
            if (Math.Abs(coldThresholdValue - value) > float.Epsilon)
            {
                Log.Tyres($"Cold threshold changed from {coldThresholdValue:0} to {value:0}");
                coldThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxColdThreshold, value);
        }
        private static void SetWarmThreshold(float value)
        {
            if (Math.Abs(warmThresholdValue - value) > float.Epsilon)
            {
                Log.Tyres($"Warm threshold changed from {warmThresholdValue:0} to {value:0}");
                warmThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxWarmThreshold, value);
        }
        private static void SetHotThreshold(float value)
        {
            if (Math.Abs(hotThresholdValue - value) > float.Epsilon)
            {
                Log.Tyres($"Hot threshold changed from {hotThresholdValue:0} to {value:0}");
                hotThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxHotThreshold, value);
        }
        private static void SetCookingThreshold(float value)
        {
            if (Math.Abs(cookingThresholdValue - value) > float.Epsilon)
            {
                Log.Tyres($"Cooking threshold changed from {cookingThresholdValue:0} to {value:0}");
                cookingThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxCookingThreshold, value);
        }
        #endregion Only run on session start or when car class changes - not every update
    }
}
