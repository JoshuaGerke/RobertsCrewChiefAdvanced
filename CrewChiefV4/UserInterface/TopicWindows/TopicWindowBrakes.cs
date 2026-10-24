using System;
using System.Drawing;
using System.Timers; // Add at the top
using System.Windows.Forms;

using CrewChiefV4.GameState;

namespace CrewChiefV4.UserInterface.TopicWindows
{
    public partial class TopicWindowBrakes : Form
    {
        private static TopicWindowBrakes _instance;
        private static int flBrakeTempCallCount = 0;
        private static System.Timers.Timer rateTimer;
        public static bool JustOpened { get; private set; }

        // cached numeric thresholds for status calculation
        private static float coldThresholdValue;
        private static float warmThresholdValue;
        private static float hotThresholdValue;
        private static float cookingThresholdValue;
        // previous statuses for logging
        private static TempStatus flPrevStatus = TempStatus.Cold;
        private static TempStatus frPrevStatus = TempStatus.Cold;
        private static TempStatus rlPrevStatus = TempStatus.Cold;
        private static TempStatus rrPrevStatus = TempStatus.Cold;
		
        public TopicWindowBrakes()
        {
            _instance = this;
            InitializeComponent();
            JustOpened = true;

            // Initialize and start the timer to update the rate every second
            rateTimer = new System.Timers.Timer(1000);
            rateTimer.Elapsed += RateTimer_Elapsed;
            rateTimer.AutoReset = true;
            rateTimer.Start();
        }

        public static void SessionConstants(in GameStateData currentGameState)
        {
            var brakeThresholds = CarData.getBrakeTempThresholds(currentGameState.carClass);
            TopicWindowBrakes.SetCarClass(currentGameState.carClass.carClassEnumString);
            TopicWindowBrakes.SetBrakeMaterial(currentGameState.carClass.brakeType.ToString());
            TopicWindowBrakes.SetColdThreshold(brakeThresholds[0].upperThreshold);
            TopicWindowBrakes.SetWarmThreshold(brakeThresholds[1].upperThreshold);
            TopicWindowBrakes.SetHotThreshold(brakeThresholds[2].upperThreshold);
            TopicWindowBrakes.SetCookingThreshold(brakeThresholds[3].upperThreshold);
        }
        private void RateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Capture and reset the count atomically
            int count = System.Threading.Interlocked.Exchange(ref flBrakeTempCallCount, 0);
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
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Brakes)))
            {
                return; // Neither Topic window nor Brake logging are active
            }
            SetTextBoxFloat(_instance?.textBoxFLTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != flPrevStatus)
            {
                Log.Brakes($"FL brake status changed from {flPrevStatus} to {newStatus} ({value:0}°C)");
                flPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxFLTemp, newStatus);
            flBrakeTempCallCount++;
            JustOpened = false; // Reset the flag after the first call
        }

        public static void SetFRTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Brakes)))
            {
                return; // Neither Topic window nor Brake logging are active
            }
            SetTextBoxFloat(_instance?.textBoxFRTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != frPrevStatus)
            {
                Log.Brakes($"FR brake status changed from {frPrevStatus} to {newStatus} ({value:0}°C)");
                frPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxFRTemp, newStatus);
        }
        public static void SetRLTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Brakes)))
            {
                return; // Neither Topic window nor Brake logging are active
            }
            SetTextBoxFloat(_instance?.textBoxRLTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != rlPrevStatus)
            {
                Log.Brakes($"RL brake status changed from {rlPrevStatus} to {newStatus} ({value:0}°C)");
                rlPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxRLTemp, newStatus);
        }
        public static void SetRRTemp(float value)
        {
            if (!(_instance != null || Log.LogTypeEnabled(Log.LogType.Brakes)))
            {
                return; // Neither Topic window nor Brake logging are active
            }
            SetTextBoxFloat(_instance?.textBoxRRTemp, value);
            var newStatus = GetTempStatus(value);
            if (newStatus != rrPrevStatus)
            {
                Log.Brakes($"RR brake status changed from {rrPrevStatus} to {newStatus} ({value:0}°C)");
                rrPrevStatus = newStatus;
            }
            SetTextBoxStatus(_instance?.textBoxRRTemp, newStatus);
        }

        #region Only run on session start or when car class changes - not every update

        private static void SetCarClass(string value)
        {
            Log.Brakes($"Car class set to '{value}'");
            SetTextBoxString(_instance?.textBoxCarClass, value == String.Empty ? "Undefined" : value);
        }
        private static void SetBrakeMaterial(string value)
        {
            Log.Brakes($"Brake material set to '{value}'");
            SetTextBoxString(_instance?.textBoxBrakeMaterial, value);
        }
        private static void SetColdThreshold(float value)
        {
            if (Math.Abs(coldThresholdValue - value) > float.Epsilon)
            {
                Log.Brakes($"Cold threshold changed from {coldThresholdValue:0} to {value:0}");
                coldThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxColdThreshold, value);
        }
        private static void SetWarmThreshold(float value)
        {
            if (Math.Abs(warmThresholdValue - value) > float.Epsilon)
            {
                Log.Brakes($"Warm threshold changed from {warmThresholdValue:0} to {value:0}");
                warmThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxWarmThreshold, value);
        }
        private static void SetHotThreshold(float value)
        {
            if (Math.Abs(hotThresholdValue - value) > float.Epsilon)
            {
                Log.Brakes($"Hot threshold changed from {hotThresholdValue:0} to {value:0}");
                hotThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxHotThreshold, value);
        }
        private static void SetCookingThreshold(float value)
        {
            if (Math.Abs(cookingThresholdValue - value) > float.Epsilon)
            {
                Log.Brakes($"Cooking threshold changed from {cookingThresholdValue:0} to {value:0}");
                cookingThresholdValue = value;
            }
            SetTextBoxFloat(_instance?.textBoxCookingThreshold, value);
        }
        #endregion Only run on session start or when car class changes - not every update
    }
}
