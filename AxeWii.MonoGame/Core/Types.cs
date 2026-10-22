using System;
using System.Collections.Generic;

namespace AxeWii.Core
{
    public enum FretId
    {
        Open,
        Green,
        Red,
        Yellow,
        Blue,
        Orange
    }

    public enum TiltZone
    {
        Lowest,   // Neck pointing steeply down (< -35 deg) -> -2 Octaves
        Low,      // Low angle (-35 to -12 deg)            -> -1 Octave
        Neutral,  // Natural playing stance (-12 to +18 deg) -> 0 (Base)
        High,     // Star Power / elevated (+18 to +50 deg) -> +1 Octave
        Highest   // Near vertical (> +50 deg)             -> +2 Octaves
    }

    public enum RollZone
    {
        Left,     // Inward roll (< -28 deg)
        Neutral,  // Center playing stance
        Right     // Outward roll (> +28 deg)
    }

    public enum ChordModifierType
    {
        Single,         // Pure root pitch (unison)
        Power5th,       // Root + Perfect 5th (0, 7)
        OctaveDouble,   // Root + 12st (0, 12)
        MajorTriad,     // Root + Major 3rd + 5th (0, 4, 7)
        MinorTriad,     // Root + Minor 3rd + 5th (0, 3, 7)
        Sus4,           // Root + 4th + 5th (0, 5, 7)
        Dom7,           // Blues/Rock 7th (0, 4, 7, 10)
        Major7,         // Lush ambient 7th (0, 4, 7, 11)
        Fourth          // Quartal interval (0, 5)
    }

    public enum HitJudgment
    {
        None,
        Perfect,
        Great,
        Good,
        Miss
    }

    public class SpatialMotionState
    {
        public float Pitch { get; set; } // -90 (down) to +90 (up)
        public float Roll { get; set; }  // -180 to +180 (body rotation)
        public float Yaw { get; set; }   // 0 to 360 compass

        public float AccelX { get; set; }
        public float AccelY { get; set; }
        public float AccelZ { get; set; }
        public float DeltaAccel { get; set; }
        public bool IsJerkTriggered { get; set; }

        public TiltZone ActiveTiltZone { get; set; } = TiltZone.Neutral;
        public int OctaveOffset { get; set; } = 0;
        public RollZone ActiveRollZone { get; set; } = RollZone.Neutral;
    }

    public class FingeringTuning
    {
        public string Id { get; set; } = "open";
        public string Label { get; set; } = "Open Strum";
        public List<FretId> Frets { get; set; } = new();
        public bool IsOpen { get; set; } = true;
        public string RootNote { get; set; } = "E2";
        public int MidiNote { get; set; } = 40;
        public int FineCents { get; set; } = 0;   // -100 to +100
        public int Transpose { get; set; } = 0;   // -24 to +24
        public ChordModifierType ChordModifier { get; set; } = ChordModifierType.Single;
    }

    public class TuningPreset
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, (string RootNote, int Cents)> Tunings { get; set; } = new();
    }

    public class MidiNoteEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public float Time { get; set; }        // In seconds
        public float Duration { get; set; }    // In seconds
        public int Pitch { get; set; }         // 0 to 127
        public int Velocity { get; set; } = 100;
        public string Fret { get; set; } = "green"; // open, green, red, yellow, blue, orange
        public List<string> MultiFrets { get; set; } = new();
        public TiltZone RequiredTiltZone { get; set; } = TiltZone.Neutral;
        public int TrackIndex { get; set; } = 0;
        public string TrackName { get; set; } = "Guitar Lead";
        public bool Hit { get; set; }
        public HitJudgment Judgment { get; set; } = HitJudgment.None;
        public bool PlayedAudio { get; set; }
        public bool StoppedAudio { get; set; }
    }

    public class MidiTrackInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = "Track";
        public string Instrument { get; set; } = "Overdriven Guitar";
        public int Channel { get; set; }
        public bool Solo { get; set; }
        public bool Muted { get; set; }
        public float Volume { get; set; } = 1.0f;
        public List<MidiNoteEvent> Events { get; set; } = new();
    }

    public class ParsedMidiSong
    {
        public string Title { get; set; } = "Untitled Song";
        public float Duration { get; set; } = 120f;
        public int Bpm { get; set; } = 120;
        public List<MidiTrackInfo> Tracks { get; set; } = new();
        public List<MidiNoteEvent> AllEvents { get; set; } = new();
    }

    public class PerformanceGrade
    {
        public int Score { get; set; }
        public int MaxCombo { get; set; }
        public int CurrentStreak { get; set; }
        public int Multiplier { get; set; } = 1;
        public int NotesHit { get; set; }
        public int TotalNotes { get; set; }
        public int PerfectCount { get; set; }
        public int GreatCount { get; set; }
        public int GoodCount { get; set; }
        public int MissCount { get; set; }

        public float Accuracy => TotalNotes > 0 ? (float)NotesHit / TotalNotes * 100f : 100f;

        public string LetterGrade
        {
            get
            {
                if (Accuracy >= 98f) return "S+";
                if (Accuracy >= 95f) return "S";
                if (Accuracy >= 90f) return "A";
                if (Accuracy >= 80f) return "B";
                if (Accuracy >= 70f) return "C";
                if (Accuracy >= 60f) return "D";
                return "F";
            }
        }
    }
}
