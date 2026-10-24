using CrewChiefV4.ACE.aceData;

using System;
using System.Collections.Generic;

namespace CrewChiefV4.TrackSpline
{
    public class TelemetrySampleCollector
    {
        public List<aceVec3> Samples { get; private set; }

        private aceVec3 _lastAccepted;
        private bool _hasLast;

        private float _minDistance;     // metres
        private float _minSpeed;        // m/s
        private float _maxIdleTime;     // seconds

        private float _idleTimer;
        private float _lapDistance;
        private aceVec3 _startPoint;
        private bool _hasStart;

        public bool LapCompleted { get; private set; }

        public TelemetrySampleCollector(
            float minDistance = 2.0f,
            float minSpeed = 5.0f,
            float maxIdleTime = 3.0f)
        {
            Samples = new List<aceVec3>();

            _minDistance = minDistance;
            _minSpeed = minSpeed;
            _maxIdleTime = maxIdleTime;
        }

        // ===============================
        // ADD SAMPLE (CALL PER TELEMETRY UPDATE)
        // ===============================
        public void AddSample(aceVec3 position, float speed, float deltaTime)
        {
            if (LapCompleted)
                return;

            // Reject if too slow (pit / stopped / spin recovery)
            if (speed < _minSpeed)
            {
                _idleTimer += deltaTime;
                return;
            }

            _idleTimer = 0f;

            if (!_hasLast)
            {
                Accept(position);
                _startPoint = position;
                _hasStart = true;
                return;
            }

            float dist = Distance(position, _lastAccepted);

            // Reject if too close (noise / oversampling)
            if (dist < _minDistance)
                return;

            _lapDistance += dist;

            Accept(position);

            // Lap completion detection
            if (_hasStart && _lapDistance > 1000f) // avoid early trigger
            {
                float toStart = Distance(position, _startPoint);

                if (toStart < 10.0f)
                {
                    LapCompleted = true;
                }
            }
        }

        private void Accept(aceVec3 pos)
        {
            Samples.Add(pos);
            _lastAccepted = pos;
            _hasLast = true;
        }

        // ===============================
        // RESET (FOR NEW LAP / SESSION)
        // ===============================
        public void Reset()
        {
            Samples.Clear();
            _hasLast = false;
            _hasStart = false;
            _idleTimer = 0f;
            _lapDistance = 0f;
            LapCompleted = false;
        }

        // ===============================
        // OPTIONAL: FORCE COMPLETE
        // ===============================
        public void ForceComplete()
        {
            LapCompleted = true;
        }

        // ===============================
        // UTILITY
        // ===============================
        private static float Distance(aceVec3 a, aceVec3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}