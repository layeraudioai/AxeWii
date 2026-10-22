using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AxeWii.Core;

namespace AxeWii.Midi
{
    /// <summary>
    /// Standard MIDI File (.mid) parser and procedural demo song generator.
    /// </summary>
    public static class MidiFileParser
    {
        public static ParsedMidiSong CreateDemoSong(string songType = "rock")
        {
            var song = new ParsedMidiSong
            {
                Title = songType == "blues" ? "AxeWii Blues Odyssey" : "Guitar Hero 3 Tribute (Rock Anthem)",
                Bpm = songType == "blues" ? 100 : 130,
                Duration = 120f
            };

            var leadTrack = new MidiTrackInfo
            {
                Index = 0,
                Name = "Guitar Lead",
                Instrument = "Overdriven Guitar",
                Channel = 0
            };

            var rand = new Random(songType.GetHashCode());
            string[] fretPool = { "open", "green", "red", "yellow", "blue", "orange" };
            float beat = 60f / song.Bpm;

            // Generate an engaging musical riff pattern
            for (float t = 0.8f; t < 118.0f; t += beat * (rand.NextDouble() > 0.4 ? 0.5f : 1.0f))
            {
                int pitch = 40 + rand.Next(30);
                string fret = fretPool[rand.Next(fretPool.Length)];
                float dur = rand.NextDouble() > 0.7 ? beat * 1.5f : beat * 0.4f;

                leadTrack.Events.Add(new MidiNoteEvent
                {
                    Time = t,
                    Duration = dur,
                    Pitch = pitch,
                    Fret = fret,
                    Velocity = 90 + rand.Next(30),
                    TrackIndex = 0,
                    TrackName = leadTrack.Name
                });
            }

            // Apply smart mapping so each note has its corresponding tilt zone & fret combination
            var summary = SmartTranscriptionMapper.MapTrack(leadTrack);
            leadTrack.Events = summary.MappedNotes;

            song.Tracks.Add(leadTrack);
            song.AllEvents = leadTrack.Events;
            return song;
        }

        public static ParsedMidiSong? ParseMidiFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                using var fs = File.OpenRead(filePath);
                using var reader = new BinaryReader(fs);

                // Read Header Chunk "MThd"
                byte[] headerTag = reader.ReadBytes(4);
                if (Encoding.ASCII.GetString(headerTag) != "MThd") return null;

                int headerLength = ReadBigEndianInt32(reader);
                int format = ReadBigEndianInt16(reader);
                int numTracks = ReadBigEndianInt16(reader);
                int division = ReadBigEndianInt16(reader);

                // Default tempo: 120 BPM = 500,000 microseconds per quarter note
                int microsecondsPerQuarter = 500000;
                int ticksPerQuarter = division > 0 ? division : 480;

                var song = new ParsedMidiSong
                {
                    Title = Path.GetFileNameWithoutExtension(filePath),
                    Bpm = 120
                };

                for (int t = 0; t < numTracks; t++)
                {
                    byte[] trackTag = reader.ReadBytes(4);
                    if (trackTag.Length < 4) break;
                    if (Encoding.ASCII.GetString(trackTag) != "MTrk")
                    {
                        // Skip non-track chunks
                        int chunkLen = ReadBigEndianInt32(reader);
                        reader.BaseStream.Seek(chunkLen, SeekOrigin.Current);
                        continue;
                    }

                    int trackLength = ReadBigEndianInt32(reader);
                    long trackEnd = reader.BaseStream.Position + trackLength;

                    var track = new MidiTrackInfo
                    {
                        Index = t,
                        Name = $"Track {t + 1}",
                        Channel = 0
                    };

                    long currentTick = 0;
                    byte runningStatus = 0;
                    var activeNotes = new Dictionary<int, (float StartTime, int Velocity)>();

                    while (reader.BaseStream.Position < trackEnd)
                    {
                        int deltaTicks = ReadVariableLength(reader);
                        currentTick += deltaTicks;

                        float currentTimeSec = (float)currentTick / ticksPerQuarter * (microsecondsPerQuarter / 1000000f);

                        byte status = reader.ReadByte();
                        if ((status & 0x80) == 0)
                        {
                            // Running status
                            reader.BaseStream.Seek(-1, SeekOrigin.Current);
                            status = runningStatus;
                        }
                        else
                        {
                            runningStatus = status;
                        }

                        byte type = (byte)(status & 0xF0);

                        if (status == 0xFF)
                        {
                            // Meta event
                            byte metaType = reader.ReadByte();
                            int metaLen = ReadVariableLength(reader);
                            byte[] metaData = reader.ReadBytes(metaLen);

                            if (metaType == 0x51 && metaLen == 3)
                            {
                                // Set Tempo
                                microsecondsPerQuarter = (metaData[0] << 16) | (metaData[1] << 8) | metaData[2];
                                song.Bpm = (int)Math.Round(60000000.0 / microsecondsPerQuarter);
                            }
                            else if (metaType == 0x03)
                            {
                                // Track Name
                                track.Name = Encoding.ASCII.GetString(metaData);
                            }
                        }
                        else if (status == 0xF0 || status == 0xF7)
                        {
                            // SysEx
                            int sysexLen = ReadVariableLength(reader);
                            reader.BaseStream.Seek(sysexLen, SeekOrigin.Current);
                        }
                        else if (type == 0x90) // Note On
                        {
                            int key = reader.ReadByte();
                            int vel = reader.ReadByte();

                            if (vel > 0)
                            {
                                activeNotes[key] = (currentTimeSec, vel);
                            }
                            else if (activeNotes.TryGetValue(key, out var start))
                            {
                                float dur = Math.Max(0.1f, currentTimeSec - start.StartTime);
                                track.Events.Add(new MidiNoteEvent
                                {
                                    Time = start.StartTime,
                                    Duration = dur,
                                    Pitch = key,
                                    Velocity = start.Velocity,
                                    TrackIndex = t,
                                    TrackName = track.Name
                                });
                                activeNotes.Remove(key);
                            }
                        }
                        else if (type == 0x80) // Note Off
                        {
                            int key = reader.ReadByte();
                            reader.ReadByte(); // release velocity

                            if (activeNotes.TryGetValue(key, out var start))
                            {
                                float dur = Math.Max(0.1f, currentTimeSec - start.StartTime);
                                track.Events.Add(new MidiNoteEvent
                                {
                                    Time = start.StartTime,
                                    Duration = dur,
                                    Pitch = key,
                                    Velocity = start.Velocity,
                                    TrackIndex = t,
                                    TrackName = track.Name
                                });
                                activeNotes.Remove(key);
                            }
                        }
                        else if (type == 0xC0 || type == 0xD0)
                        {
                            reader.ReadByte(); // 1-data byte messages
                        }
                        else
                        {
                            reader.ReadByte(); // 2-data byte messages
                            reader.ReadByte();
                        }
                    }

                    if (track.Events.Count > 0)
                    {
                        var mapped = SmartTranscriptionMapper.MapTrack(track);
                        track.Events = mapped.MappedNotes;
                        song.Tracks.Add(track);
                    }
                }

                // Combine all events
                var all = new List<MidiNoteEvent>();
                foreach (var trk in song.Tracks) all.AddRange(trk.Events);
                all.Sort((a, b) => a.Time.CompareTo(b.Time));
                song.AllEvents = all;

                if (song.AllEvents.Count > 0)
                {
                    song.Duration = song.AllEvents[^1].Time + song.AllEvents[^1].Duration + 3.0f;
                }

                return song;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AxeWii] MIDI Parse error: {ex.Message}");
                return null;
            }
        }

        private static int ReadBigEndianInt32(BinaryReader r)
        {
            byte[] b = r.ReadBytes(4);
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }

        private static int ReadBigEndianInt16(BinaryReader r)
        {
            byte[] b = r.ReadBytes(2);
            return (b[0] << 8) | b[1];
        }

        private static int ReadVariableLength(BinaryReader r)
        {
            int value = 0;
            byte b;
            do
            {
                b = r.ReadByte();
                value = (value << 7) | (b & 0x7F);
            } while ((b & 0x80) != 0);
            return value;
        }
    }
}
