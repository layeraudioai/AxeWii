using System;
using System.Collections.Generic;
using System.Linq;
using AxeWii.Core;
using AxeWii.Hardware;

namespace AxeWii.Tuning
{
    /// <summary>
    /// Complete Fingering & Chord Tuning Manager for all 32 guitar fret combinations.
    /// Supports real-time analog thumbstick nudging (Stick Y fine cents, Stick X chord modifier cycling),
    /// persistent tuning states, and multi-voice chord pitch generation.
    /// </summary>
    public class FingeringManager
    {
        private readonly Dictionary<string, FingeringTuning> _tunings = new();
        private DateTime _lastStickXTime = DateTime.MinValue;
        private DateTime _lastStickYTime = DateTime.MinValue;

        public event Action<FingeringTuning>? OnTuningChanged;

        public FingeringManager()
        {
            InitializeDefaultTunings();
        }

        private void InitializeDefaultTunings()
        {
            // 1. Open Strum
            SetTuning(new FingeringTuning
            {
                Id = "open",
                Label = "Open Strum",
                IsOpen = true,
                RootNote = "E2",
                MidiNote = 40,
                FineCents = 0,
                Transpose = 0,
                ChordModifier = ChordModifierType.Single
            });

            // 2. 5 Single Frets
            var singleFrets = new[]
            {
                (FretId.Green, "green", "A2", 45),
                (FretId.Red, "red", "D3", 50),
                (FretId.Yellow, "yellow", "G3", 55),
                (FretId.Blue, "blue", "B3", 59),
                (FretId.Orange, "orange", "E4", 64)
            };

            foreach (var (fret, id, note, midi) in singleFrets)
            {
                SetTuning(new FingeringTuning
                {
                    Id = id,
                    Label = char.ToUpper(id[0]) + id.Substring(1) + " Fret",
                    Frets = new List<FretId> { fret },
                    IsOpen = false,
                    RootNote = note,
                    MidiNote = midi,
                    FineCents = 0,
                    Transpose = 0,
                    ChordModifier = ChordModifierType.Single
                });
            }

            // 3. Common Dyads (2-fret combinations)
            var dyads = new[]
            {
                (new[] { FretId.Green, FretId.Red }, "green+red", "A2", 45, ChordModifierType.Power5th),
                (new[] { FretId.Green, FretId.Yellow }, "green+yellow", "A2", 45, ChordModifierType.MinorTriad),
                (new[] { FretId.Green, FretId.Blue }, "green+blue", "A2", 45, ChordModifierType.OctaveDouble),
                (new[] { FretId.Green, FretId.Orange }, "green+orange", "A2", 45, ChordModifierType.Dom7),
                (new[] { FretId.Red, FretId.Yellow }, "red+yellow", "D3", 50, ChordModifierType.Power5th),
                (new[] { FretId.Red, FretId.Blue }, "red+blue", "D3", 50, ChordModifierType.MajorTriad),
                (new[] { FretId.Red, FretId.Orange }, "red+orange", "D3", 50, ChordModifierType.OctaveDouble),
                (new[] { FretId.Yellow, FretId.Blue }, "yellow+blue", "G3", 55, ChordModifierType.Power5th),
                (new[] { FretId.Yellow, FretId.Orange }, "yellow+orange", "G3", 55, ChordModifierType.MajorTriad),
                (new[] { FretId.Blue, FretId.Orange }, "blue+orange", "B3", 59, ChordModifierType.Power5th),
            };

            foreach (var (frets, id, note, midi, chord) in dyads)
            {
                SetTuning(new FingeringTuning
                {
                    Id = id,
                    Label = FormatFingeringLabel(id),
                    Frets = frets.ToList(),
                    IsOpen = false,
                    RootNote = note,
                    MidiNote = midi,
                    FineCents = 0,
                    Transpose = 0,
                    ChordModifier = chord
                });
            }
        }

        public void SetTuning(FingeringTuning tuning)
        {
            _tunings[tuning.Id] = tuning;
        }

        public static string GetFingeringId(GuitarFrets frets)
        {
            if (!frets.AnyHeld) return "open";
            var active = new List<string>();
            if (frets.Green) active.Add("green");
            if (frets.Red) active.Add("red");
            if (frets.Yellow) active.Add("yellow");
            if (frets.Blue) active.Add("blue");
            if (frets.Orange) active.Add("orange");
            return string.Join("+", active);
        }

        public static string FormatFingeringLabel(string id)
        {
            if (id == "open") return "Open Strum";
            return string.Join(" + ", id.Split('+').Select(s => char.ToUpper(s[0]) + s.Substring(1)));
        }

        public FingeringTuning GetTuning(string id)
        {
            if (!_tunings.TryGetValue(id, out var tuning))
            {
                var parts = id.Split('+');
                int defaultMidi = 45 + parts.Length * 3;
                tuning = new FingeringTuning
                {
                    Id = id,
                    Label = FormatFingeringLabel(id),
                    IsOpen = id == "open",
                    RootNote = MusicTheory.MidiToNoteName(defaultMidi),
                    MidiNote = defaultMidi,
                    FineCents = 0,
                    Transpose = 0,
                    ChordModifier = parts.Length > 1 ? ChordModifierType.Power5th : ChordModifierType.Single
                };
                _tunings[id] = tuning;
            }
            return tuning;
        }

        public List<FingeringTuning> GetAllTunings() => _tunings.Values.ToList();

        public void ApplyPreset(TuningPreset preset)
        {
            foreach (var kvp in preset.Tunings)
            {
                var tuning = GetTuning(kvp.Key);
                tuning.RootNote = kvp.Value.RootNote;
                tuning.MidiNote = MusicTheory.NoteNameToMidi(kvp.Value.RootNote);
                tuning.FineCents = kvp.Value.Cents;
                tuning.Transpose = 0;
                _tunings[kvp.Key] = tuning;
            }
        }

        /// <summary>
        /// Real-time thumbstick fine-tuning and chord mod cycling:
        /// - Stick Y: fine cents (-100 to +100) or semitone transpose
        /// - Stick X: cycle chord modifiers (Single, Power 5th, Major, Minor, Sus4, Dom7, etc.)
        /// </summary>
        public bool HandleStickInput(string fingeringId, float stickX, float stickY, out string actionMessage)
        {
            actionMessage = "";
            var tuning = GetTuning(fingeringId);
            bool changed = false;
            var now = DateTime.UtcNow;

            // 1. Stick Y: Fine Cents Nudge
            if (Math.Abs(stickY) > 0.25f)
            {
                int intervalMs = Math.Abs(stickY) > 0.7f ? 60 : 120;
                if ((now - _lastStickYTime).TotalMilliseconds > intervalMs)
                {
                    _lastStickYTime = now;
                    int delta = stickY > 0 ? 1 : -1;
                    int nextCents = tuning.FineCents + delta;
                    int nextTranspose = tuning.Transpose;

                    if (nextCents > 100)
                    {
                        nextCents = -99;
                        nextTranspose = Math.Min(24, nextTranspose + 1);
                    }
                    else if (nextCents < -100)
                    {
                        nextCents = 99;
                        nextTranspose = Math.Max(-24, nextTranspose - 1);
                    }

                    tuning.FineCents = nextCents;
                    tuning.Transpose = nextTranspose;
                    changed = true;
                    actionMessage = $"Fine Tuned {tuning.Label}: {(tuning.FineCents >= 0 ? "+" : "")}{tuning.FineCents}¢ (Trans: {tuning.Transpose})";
                }
            }

            // 2. Stick X: Cycle Chord Modifiers
            if (Math.Abs(stickX) > 0.6f)
            {
                if ((now - _lastStickXTime).TotalMilliseconds > 320)
                {
                    _lastStickXTime = now;
                    int total = MusicTheory.ChordModifiers.Length;
                    int currentIdx = Array.FindIndex(MusicTheory.ChordModifiers, m => m.Type == tuning.ChordModifier);
                    if (currentIdx == -1) currentIdx = 0;

                    int nextIdx = stickX > 0 ? (currentIdx + 1) % total : (currentIdx - 1 + total) % total;
                    tuning.ChordModifier = MusicTheory.ChordModifiers[nextIdx].Type;
                    changed = true;
                    actionMessage = $"Chord Mod: {tuning.Label} -> {MusicTheory.ChordModifiers[nextIdx].Label}";
                }
            }

            if (changed)
            {
                _tunings[fingeringId] = tuning;
                OnTuningChanged?.Invoke(tuning);
            }

            return changed;
        }

        public (List<int> Pitches, int FineCents, string ChordName) GetChordPitches(string fingeringId, int octaveOffset = 0)
        {
            var tuning = GetTuning(fingeringId);
            int baseMidi = tuning.MidiNote + tuning.Transpose + octaveOffset * 12;

            var modInfo = MusicTheory.ChordModifiers.FirstOrDefault(m => m.Type == tuning.ChordModifier) 
                          ?? MusicTheory.ChordModifiers[0];

            var pitches = modInfo.SemitoneIntervals
                .Select(interval => Math.Clamp(baseMidi + interval, 12, 127))
                .ToList();

            return (pitches, tuning.FineCents, modInfo.Label);
        }

        public void ResetAllFineTunings()
        {
            foreach (var tuning in _tunings.Values)
            {
                tuning.FineCents = 0;
                tuning.Transpose = 0;
            }
        }
    }
}
