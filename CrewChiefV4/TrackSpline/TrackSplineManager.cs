using CrewChiefV4.ACE.aceData;
using CrewChiefV4.GameState;

using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.TrackSpline
{
    public static class TrackSplineManager
    {
        static TelemetrySampleCollector _collector;
        static TrackSpline spline;
        static string trackName;
        /// <summary>
        /// Initializes the track spline for the current game session, loading existing data or preparing to collect new
        /// telemetry samples if unavailable.
        /// </summary>
        public static void Start(GameStateData currentGameState)
        {
            trackName = currentGameState.SessionData.TrackDefinition.name;
            spline = TrackSpline.Load(trackName);
            if (spline != null)
            {
                Log.Commentary($"Loaded spline for {trackName} with {spline.nodes.Count} nodes and calculated length {spline.totalLength:0}m vs. {currentGameState.SessionData.TrackDefinition.trackLength:0}m reported by game");
            }
            else
            {
                Log.Warning($"No spline for {trackName}, creating new one");
                _collector = new TelemetrySampleCollector();
            }
        }

        /// <summary>
        /// Add a sample to the telemetry collector and check for lap completion. If a lap is completed, build and save the track spline.
        /// </summary>
        /// <param name="currentGameState">The current game state containing session and track information.</param>
        /// <param name="pos">The current position of the car.</param>
        /// <param name="deltaTime">The time elapsed since the last update.</param>
        public static void Update(GameStateData currentGameState, aceVec3 pos, float deltaTime)
        {
            if (_collector != null)
            {
                _collector.AddSample(pos, 20, deltaTime); // TBD EVO currentGameState.PositionAndMotionData.CarSpeed, deltaTime);
                if (_collector.LapCompleted)
                {
                    Log.Commentary($"Lap completed with {_collector.Samples.Count} samples, building spline...");
                    spline = TrackSpline.Build(_collector.Samples, 2.0f);
                    spline.Save(currentGameState.SessionData.TrackDefinition.name);
                    Log.Commentary($"Spline saved with {spline.nodes.Count} nodes and total length {spline.totalLength:0}m vs. {currentGameState.SessionData.TrackDefinition.trackLength:0}m reported by game");
                    _collector = null;
                }
            }
        }
        public static TrackSpline.SplineProjection Project(aceVec3 pos, float previous)
        {
            if (spline != null)
            {
                return spline.Project(pos, previous);
            }
            return new TrackSpline.SplineProjection(0f, 0f, false);
        }

        public class SplineViewerDialog : Form
        {
            private SplineViewerControl _viewer;

            /// <summary>
            /// Dialog to visualize the current spline.
            /// </summary>
            public SplineViewerDialog()
            {
                this.Text = $"Track outline of {trackName}. Press Esc to close.";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterParent;
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
                this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));

                _viewer = new SplineViewerControl
                {
                    Dock = DockStyle.Fill,
                    Spline = spline
                };

                this.Controls.Add(_viewer);

                // Optional: close on ESC
                this.KeyPreview = true;
                this.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                        this.Close();
                };
            }
        }
    }
}