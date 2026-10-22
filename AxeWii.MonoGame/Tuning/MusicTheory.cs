using System;
using System.Collections.Generic;
using AxeWii.Core;

namespace AxeWii.Tuning
{
    public class ChordModifierInfo
    {
        public ChordModifierType Type { get; set; }
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public int[] SemitoneIntervals { get; set; } = Array.Empty<int>();
    }

    public static class MusicTheory
    {
        public static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static readonly ChordModifierInfo[] ChordModifiers = new[]
        {
            new ChordModifierInfo
            {
                Type = ChordModifierType.Single,
                Label = "Single Note / Unison",
                Description = "Pure root pitch without extra voices",
                SemitoneIntervals = new[] { 0 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.Power5th,
                Label = "Power Chord (5th)",
                Description = "Heavy rock power chord (Root + Perfect 5th)",
                SemitoneIntervals = new[] { 0, 7 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.OctaveDouble,
                Label = "Octave Double",
                Description = "Lead guitar octave stack (Root + 12st)",
                SemitoneIntervals = new[] { 0, 12 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.MajorTriad,
                Label = "Major Triad",
                Description = "Bright harmonic chord (Root + Major 3rd + 5th)",
                SemitoneIntervals = new[] { 0, 4, 7 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.MinorTriad,
                Label = "Minor Triad",
                Description = "Moody expressive chord (Root + Minor 3rd + 5th)",
                SemitoneIntervals = new[] { 0, 3, 7 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.Sus4,
                Label = "Suspended 4th (Sus4)",
                Description = "Tension-building chord (Root + 4th + 5th)",
                SemitoneIntervals = new[] { 0, 5, 7 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.Dom7,
                Label = "Dominant 7th",
                Description = "Blues/Rock 7th chord (Root + 3rd + 5th + Flat 7th)",
                SemitoneIntervals = new[] { 0, 4, 7, 10 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.Major7,
                Label = "Major 7th",
                Description = "Lush jazz/ambient chord (Root + 3rd + 5th + Maj 7th)",
                SemitoneIntervals = new[] { 0, 4, 7, 11 }
            },
            new ChordModifierInfo
            {
                Type = ChordModifierType.Fourth,
                Label = "Fourth Interval (4th)",
                Description = "Hard rock quartal interval (Root + 5st)",
                SemitoneIntervals = new[] { 0, 5 }
            }
        };

        public static string MidiToNoteName(int midi)
        {
            int noteIndex = ((midi % 12) + 12) % 12;
            int octave = (int)Math.Floor(midi / 12.0) - 1;
            return $"{NoteNames[noteIndex]}{octave}";
        }

        public static int NoteNameToMidi(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 60;
            name = name.Trim().ToUpper();

            string notePart;
            int octave = 3;

            if (name.Length >= 2 && (name[1] == '#' || name[1] == 'B'))
            {
                notePart = name.Substring(0, 2);
                if (int.TryParse(name.Substring(2), out int parsedOct)) octave = parsedOct;
            }
            else
            {
                notePart = name.Substring(0, 1);
                if (int.TryParse(name.Substring(1), out int parsedOct)) octave = parsedOct;
            }

            if (notePart == "DB") notePart = "C#";
            if (notePart == "EB") notePart = "D#";
            if (notePart == "GB") notePart = "F#";
            if (notePart == "AB") notePart = "G#";
            if (notePart == "BB") notePart = "A#";

            int idx = Array.IndexOf(NoteNames, notePart);
            if (idx == -1) idx = 0;
            return (octave + 1) * 12 + idx;
        }

        public static float MidiToFrequency(int midi, float fineCents = 0)
        {
            float totalMidi = midi + fineCents / 100f;
            return 440f * MathF.Pow(2f, (totalMidi - 69f) / 12f);
        }

        public static readonly List<TuningPreset> TuningPresets = new()
        {
            new TuningPreset
            {
                Name = "Guitar Standard E",
                Description = "Classic 6-string guitar tuning (Open E2, G:A2, R:D3, Y:G3, B:B3, O:E4)",
                Tunings = new Dictionary<string, (string, int)>
                {
                    ["open"] = ("E2", 0),
                    ["green"] = ("A2", 0),
                    ["red"] = ("D3", 0),
                    ["yellow"] = ("G3", 0),
                    ["blue"] = ("B3", 0),
                    ["orange"] = ("E4", 0)
                }
            },
            new TuningPreset
            {
                Name = "Minor Pentatonic (Rock/Blues)",
                Description = "Solo-ready pentatonic minor scale in A (A2, C3, D3, E3, G3, A3)",
                Tunings = new Dictionary<string, (string, int)>
                {
                    ["open"] = ("A2", 0),
                    ["green"] = ("C3", 0),
                    ["red"] = ("D3", 0),
                    ["yellow"] = ("E3", 0),
                    ["blue"] = ("G3", 0),
                    ["orange"] = ("A3", 0)
                }
            },
            new TuningPreset
            {
                Name = "Drop D Heavy Power",
                Description = "Heavy dropped power chord register (Open D2, G:A2, R:D3, Y:F#3, B:A3, O:D4)",
                Tunings = new Dictionary<string, (string, int)>
                {
                    ["open"] = ("D2", 0),
                    ["green"] = ("A2", 0),
                    ["red"] = ("D3", 0),
                    ["yellow"] = ("F#3", 0),
                    ["blue"] = ("A3", 0),
                    ["orange"] = ("D4", 0)
                }
            },
            new TuningPreset
            {
                Name = "Blues Scale (with Blue Note)",
                Description = "Classic 6-note blues scale with the diminished fifth flat five",
                Tunings = new Dictionary<string, (string, int)>
                {
                    ["open"] = ("E3", 0),
                    ["green"] = ("G3", 0),
                    ["red"] = ("A3", 0),
                    ["yellow"] = ("A#3", 0),
                    ["blue"] = ("B3", 0),
                    ["orange"] = ("D4", 0)
                }
            },
            new TuningPreset
            {
                Name = "Synth Bass Octaves",
                Description = "Deep sub bass and multi-octave punch for electronic basslines (C1 to C3)",
                Tunings = new Dictionary<string, (string, int)>
                {
                    ["open"] = ("C1", 0),
                    ["green"] = ("G1", 0),
                    ["red"] = ("C2", 0),
                    ["yellow"] = ("E2", 0),
                    ["blue"] = ("G2", 0),
                    ["orange"] = ("C3", 0)
                }
            },
            new TuningPreset
            {
                Name = "Microtonal 24-TET / Quarter-Tones",
                Description = "Quarter-tone microtonal tuning (+50 cents intervals for experimental scales)",
                Tunings = new Dictionary<string, (string, int)>
                {
                    ["open"] = ("E3", 0),
                    ["green"] = ("E3", 50),
                    ["red"] = ("F3", 0),
                    ["yellow"] = ("F3", 50),
                    ["blue"] = ("F#3", 0),
                    ["orange"] = ("G3", 0)
                }
            }
        };
    }
}
