using System;
using System.Collections.Generic;
using System.Linq;
using AxeWii.Core;
using AxeWii.Tuning;

namespace AxeWii.Midi
{
    public class TranscriptionSummary
    {
        public string TrackName { get; set; } = "";
        public int TotalNotes { get; set; }
        public int MinPitch { get; set; }
        public int MaxPitch { get; set; }
        public int OctaveSpan { get; set; }
        public List<MidiNoteEvent> MappedNotes { get; set; } = new();
    }

    /// <summary>
    /// Smart Transcription & Auto-Mapping Engine for C#/MonoGame.
    /// Automatically maps imported MIDI notes across all octaves to exact
    /// (Fret + Neck Tilt Zone) combinations, ensuring 100% complete transcription
    /// with zero missing notes for any octave.
    /// </summary>
    public static class SmartTranscriptionMapper
    {
        public static TranscriptionSummary MapTrack(MidiTrackInfo track)
        {
            if (track.Events == null || track.Events.Count == 0)
            {
                return new TranscriptionSummary
                {
                    TrackName = track.Name,
                    TotalNotes = 0
                };
            }

            var uniquePitches = track.Events.Select(e => e.Pitch).Distinct().OrderBy(p => p).ToList();
            int minPitch = uniquePitches.First();
            int maxPitch = uniquePitches.Last();
            int pitchSpan = maxPitch - minPitch;
            int octaveSpan = (int)Math.Ceiling(pitchSpan / 12.0);

            // Median pitch anchor for the neutral stance
            int medianPitch = uniquePitches[uniquePitches.Count / 2];

            var mappedList = new List<MidiNoteEvent>();

            foreach (var note in track.Events)
            {
                int deltaFromMedian = note.Pitch - medianPitch;
                TiltZone zone = TiltZone.Neutral;

                if (deltaFromMedian <= -15) zone = TiltZone.Lowest;
                else if (deltaFromMedian <= -6) zone = TiltZone.Low;
                else if (deltaFromMedian <= 7) zone = TiltZone.Neutral;
                else if (deltaFromMedian <= 17) zone = TiltZone.High;
                else zone = TiltZone.Highest;

                // Scale-degree modulo mapping
                int pitchMod = ((note.Pitch % 12) + 12) % 12;
                string fret = "green";
                var multiFrets = new List<string>();

                // 0, 1 -> Open Strum
                // 2, 3 -> Green
                // 4, 5 -> Red
                // 6, 7 -> Yellow
                // 8, 9 -> Blue
                // 10, 11 -> Orange
                if (pitchMod == 0 || pitchMod == 1)
                {
                    fret = "open";
                }
                else if (pitchMod == 2 || pitchMod == 3)
                {
                    fret = "green";
                }
                else if (pitchMod == 4 || pitchMod == 5)
                {
                    fret = "red";
                }
                else if (pitchMod == 6 || pitchMod == 7)
                {
                    fret = "yellow";
                }
                else if (pitchMod == 8 || pitchMod == 9)
                {
                    fret = "blue";
                }
                else
                {
                    fret = "orange";
                }

                // Chromatic semitone representations (2-fret combinations)
                if (pitchMod == 1) multiFrets = new List<string> { "green", "red" };
                else if (pitchMod == 3) multiFrets = new List<string> { "green", "yellow" };
                else if (pitchMod == 6) multiFrets = new List<string> { "red", "yellow" };
                else if (pitchMod == 8) multiFrets = new List<string> { "yellow", "blue" };
                else if (pitchMod == 10) multiFrets = new List<string> { "blue", "orange" };

                var mappedNote = new MidiNoteEvent
                {
                    Id = note.Id,
                    Time = note.Time,
                    Duration = note.Duration,
                    Pitch = note.Pitch,
                    Velocity = note.Velocity,
                    Fret = fret,
                    MultiFrets = multiFrets,
                    RequiredTiltZone = zone,
                    TrackIndex = track.Index,
                    TrackName = track.Name
                };

                mappedList.Add(mappedNote);
            }

            return new TranscriptionSummary
            {
                TrackName = track.Name,
                TotalNotes = mappedList.Count,
                MinPitch = minPitch,
                MaxPitch = maxPitch,
                OctaveSpan = octaveSpan,
                MappedNotes = mappedList
            };
        }
    }
}
