using System;
using AxeWii.Core;

namespace AxeWii.Motion
{
    /// <summary>
    /// Real-time 3D Spatial Motion & Accelerometer Processor for Wiimote.
    /// Calculates neck pitch elevation, body roll, 5 tilt octave zones, jerk strike, and tare calibration.
    /// </summary>
    public class MotionProcessor
    {
        public SpatialMotionState State { get; private set; } = new();

        private float _tarePitch = 0f;
        private float _tareRoll = 0f;
        private float _lastAccelMagnitude = 1.0f;
        private DateTime _lastTime = DateTime.UtcNow;

        // Octave pitch threshold constants
        public const float ThresholdLowest = -35f;
        public const float ThresholdLow = -12f;
        public const float ThresholdNeutral = 18f;
        public const float ThresholdHigh = 50f;

        // Roll thresholds
        public const float ThresholdRollLeft = -28f;
        public const float ThresholdRollRight = 28f;

        public void Tare()
        {
            _tarePitch = State.Pitch + _tarePitch;
            _tareRoll = State.Roll + _tareRoll;
        }

        public void ResetTare()
        {
            _tarePitch = 0f;
            _tareRoll = 0f;
        }

        public SpatialMotionState Process(float ax, float ay, float az)
        {
            var now = DateTime.UtcNow;
            float dt = Math.Clamp((float)(now - _lastTime).TotalSeconds, 0.001f, 0.1f);
            _lastTime = now;

            // Compute pitch elevation (neck tilt up/down)
            // ax: left/right, ay: down neck, az: perpendicular to face
            float horiz = MathF.Sqrt(ax * ax + az * az);
            float rawPitch = MathF.Atan2(ay, Math.Max(0.001f, horiz)) * (180f / MathF.PI);
            float rawRoll = MathF.Atan2(ax, Math.Max(0.001f, az)) * (180f / MathF.PI);

            // Apply tare offset
            float pitch = rawPitch - _tarePitch;
            float roll = rawRoll - _tareRoll;

            // Jerk / Delta acceleration calculation
            float currentMag = MathF.Sqrt(ax * ax + ay * ay + az * az);
            float jerk = Math.Abs(currentMag - _lastAccelMagnitude) / dt;
            _lastAccelMagnitude = currentMag;

            // Determine Tilt Zone & Octave Offset
            TiltZone tiltZone;
            int octaveOffset;

            if (pitch <= ThresholdLowest)
            {
                tiltZone = TiltZone.Lowest;
                octaveOffset = -2;
            }
            else if (pitch <= ThresholdLow)
            {
                tiltZone = TiltZone.Low;
                octaveOffset = -1;
            }
            else if (pitch <= ThresholdNeutral)
            {
                tiltZone = TiltZone.Neutral;
                octaveOffset = 0;
            }
            else if (pitch <= ThresholdHigh)
            {
                tiltZone = TiltZone.High;
                octaveOffset = 1;
            }
            else
            {
                tiltZone = TiltZone.Highest;
                octaveOffset = 2;
            }

            // Determine Roll Zone
            RollZone rollZone = roll <= ThresholdRollLeft ? RollZone.Left :
                                roll >= ThresholdRollRight ? RollZone.Right : RollZone.Neutral;

            State.Pitch = pitch;
            State.Roll = roll;
            State.AccelX = ax;
            State.AccelY = ay;
            State.AccelZ = az;
            State.DeltaAccel = jerk;
            State.IsJerkTriggered = jerk > 2.2f;
            State.ActiveTiltZone = tiltZone;
            State.OctaveOffset = octaveOffset;
            State.ActiveRollZone = rollZone;

            return State;
        }
    }
}
