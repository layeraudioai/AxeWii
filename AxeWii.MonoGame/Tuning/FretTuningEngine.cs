using System;
using System.Collections.Generic;

namespace AxeWii.Tuning
{
    public class FretDefinition
    {
        public string NoteName { get; set; } = "E";
        public int Octave { get; set; } = 3;
        public int FineCents { get; set; } = 0;

        public int BaseMidiNote => (Octave + 1) * 12 + NoteNameToSemitone(NoteName);

        public static int NoteNameToSemitone(string name)
        {
            return name.Trim().ToUpper() switch
            {
                "C" => 0, "C#" => 1, "DB" => 1,
                "D" => 2, "D#" => 3, "EB" => 3,
                "E" => 4,
                "F" => 5, "F#" => 6, "GB" => 6,
                "G" => 7, "G#" => 8, "AB" => 8,
                "A" => 9, "A#" => 10, "BB" => 10,
                "B" => 11,
                _ => 0
            };
        }
    }

    /// <summary>
    /// Fret Tuning & Persistent Thumbstick Tuning Engine.
    /// Remembers fine tuning for open note, individual frets, and every possible chord combination.
    /// Plus (+) button resets fine tunings; Minus (-) button tares motion stance.
    /// </summary>
    public class FretTuningEngine
    {
        public Dictionary<string, FretDefinition> BaseFretTunings { get; set; } = new()
        {
            ["open"] = new FretDefinition { NoteName = "E", Octave = 2 },
            ["green"] = new FretDefinition { NoteName = "E", Octave = 3 },
            ["red"] = new FretDefinition { NoteName = "A", Octave = 3 },
            ["yellow"] = new FretDefinition { NoteName = "D", Octave = 4 },
            ["blue"] = new FretDefinition { NoteName = "G", Octave = 4 },
            ["orange"] = new FretDefinition { NoteName = "B", Octave = 4 },
        };

        // Persistent chord modifications dictionary: chordKey -> fineCentsOffset
        private readonly Dictionary<string, int> _chordFineTunings = new();

        // Spatial motion tare state
        public float TarePitch { get; private set; } = 0f;
        public float TareRoll { get; private set; } = 0f;
        public int OctaveShift { get; set; } = 0;

        public event Action<string, int>? OnTuningModified;

        public void ApplyStickFineTune(string chordKey, int deltaCents)
        {
            if (!_chordFineTunings.ContainsKey(chordKey))
            {
                _chordFineTunings[chordKey] = 0;
            }
            _chordFineTunings[chordKey] = Math.Clamp(_chordFineTunings[chordKey] + deltaCents, -1200, 1200);
            OnTuningModified?.Invoke(chordKey, _chordFineTunings[chordKey]);
        }

        public (int MidiNote, int FineCents) GetSoundParameters(string chordKey, string primaryFret)
        {
            int baseMidi = 40; // E2 default
            int fine = 0;

            if (BaseFretTunings.TryGetValue(primaryFret.ToLower(), out var def))
            {
                baseMidi = def.BaseMidiNote;
                fine = def.FineCents;
            }

            // Apply persistent chord modification
            if (_chordFineTunings.TryGetValue(chordKey, out var chordOffset))
            {
                fine += chordOffset;
            }

            // Apply spatial octave modifier
            baseMidi += OctaveShift * 12;

            return (baseMidi, fine);
        }

        public void TareMotionStance(float currentPitch, float currentRoll)
        {
            TarePitch = currentPitch;
            TareRoll = currentRoll;
        }

        public void ResetAllFineTunings()
        {
            _chordFineTunings.Clear();
            foreach (var kvp in BaseFretTunings)
            {
                kvp.Value.FineCents = 0;
            }
        }
    }
}
