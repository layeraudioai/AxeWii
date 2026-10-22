using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using MeltySynth;

namespace AxeWii.Audio
{
    public enum GuitarTone
    {
        DistortionGuitar, // GM 30
        OverdriveGuitar,   // GM 29
        ElectricClean,     // GM 27
        AcousticSteel,     // GM 25
        CustomSoundFont
    }

    /// <summary>
    /// SoundFont 2 (SF2) & General MIDI Synthesizer Engine for MonoGame / C#.
    /// Provides low-latency real-time audio playback through DynamicSoundEffectInstance
    /// with SF2 soundbank importing, MeltySynth playback, and procedural guitar fallback.
    /// </summary>
    public class SoundFontSynthEngine : IDisposable
    {
        private Synthesizer? _synth;
        private SoundFont? _soundFont;
        private DynamicSoundEffectInstance? _dynamicSound;

        public GuitarTone CurrentTone { get; private set; } = GuitarTone.OverdriveGuitar;
        public float MasterVolume { get; set; } = 0.85f;
        public float WhammyBendSemitones { get; set; } = 0f;

        public string? LoadedSoundFontName { get; private set; }
        public string? LoadedSoundFontPath { get; private set; }
        public List<string> PresetNames { get; private set; } = new();
        public int CurrentPresetIndex { get; private set; } = 0;
        public string CurrentPresetName => PresetNames.Count > 0 && CurrentPresetIndex < PresetNames.Count 
            ? PresetNames[CurrentPresetIndex] : (IsLoaded ? "SoundFont Active" : "Built-in Procedural GM");

        private const int SampleRate = 44100;
        private const int BufferLength = 512;
        private readonly float[] _leftBuffer = new float[BufferLength];
        private readonly float[] _rightBuffer = new float[BufferLength];
        private readonly byte[] _pcmBuffer = new byte[BufferLength * 2 * 2]; // 16-bit stereo PCM

        // Procedural guitar synth fallback when no SF2 is loaded
        private class ProceduralVoice
        {
            public int Key;
            public float Frequency;
            public float Phase;
            public float Amplitude;
            public float Decay;
            public float Harmonics;
            public bool Active;
        }

        private readonly List<ProceduralVoice> _proceduralVoices = new();

        public bool IsLoaded => _synth != null;

        public void Initialize()
        {
            try
            {
                // 1. Look for default SoundFont files (.sf2) in app folder or SoundFonts/ directory
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] searchDirs = {
                    baseDir,
                    Path.Combine(baseDir, "SoundFonts"),
                    Path.Combine(baseDir, "Content")
                };

                string? foundPath = null;
                foreach (var dir in searchDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.sf2", SearchOption.TopDirectoryOnly);
                        if (files.Length > 0)
                        {
                            foundPath = files[0];
                            break;
                        }
                    }
                }

                if (foundPath != null)
                {
                    LoadSoundFont(foundPath);
                }

                // 2. Setup MonoGame real-time DynamicSoundEffectInstance audio output
                _dynamicSound = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Stereo);
                _dynamicSound.BufferNeeded += (sender, e) => StreamAudioChunk();

                // Prime initial buffers BEFORE calling Play() to prevent startup underrun
                for (int i = 0; i < 4; i++)
                {
                    StreamAudioChunk();
                }
                _dynamicSound.Play();

                if (!IsLoaded)
                {
                    SetTone(GuitarTone.OverdriveGuitar);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AxeWii] Synth init note: {ex.Message}");
            }
        }

        public bool LoadSoundFont(string sf2Path)
        {
            try
            {
                if (!File.Exists(sf2Path)) return false;

                _soundFont = new SoundFont(sf2Path);
                var settings = new SynthesizerSettings(SampleRate)
                {
                    BlockSize = 64,
                    MaximumPolyphony = 64,
                    EnableReverbAndChorus = true
                };
                _synth = new Synthesizer(_soundFont, settings);
                CurrentTone = GuitarTone.CustomSoundFont;
                LoadedSoundFontPath = sf2Path;
                LoadedSoundFontName = Path.GetFileName(sf2Path);

                PresetNames.Clear();
                foreach (var p in _soundFont.Presets)
                {
                    PresetNames.Add($"{p.PatchNumber:D3}:{p.BankNumber:D3} - {p.Name}");
                }

                if (PresetNames.Count > 0)
                {
                    SelectPreset(0);
                }

                Console.WriteLine($"[AxeWii] Loaded SoundFont: {LoadedSoundFontName} with {PresetNames.Count} presets.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AxeWii] Failed to load SoundFont: {ex.Message}");
                return false;
            }
        }

        public void SelectPreset(int index)
        {
            if (_soundFont == null || _synth == null || _soundFont.Presets.Count == 0) return;
            CurrentPresetIndex = Math.Clamp(index, 0, _soundFont.Presets.Count - 1);
            var preset = _soundFont.Presets[CurrentPresetIndex];

            // Bank Select (CC 0)
            _synth.ProcessMidiMessage(0, 0xB0, 0x00, preset.BankNumber);
            // Program Change (0xC0)
            _synth.ProcessMidiMessage(0, 0xC0, preset.PatchNumber, 0);
        }

        public void CyclePreset(int direction)
        {
            if (PresetNames.Count == 0) return;
            int next = (CurrentPresetIndex + direction + PresetNames.Count) % PresetNames.Count;
            SelectPreset(next);
        }

        public void SetTone(GuitarTone tone)
        {
            CurrentTone = tone;
            if (_synth == null) return;

            int program = tone switch
            {
                GuitarTone.DistortionGuitar => 30, // GM 30: Distortion Guitar
                GuitarTone.OverdriveGuitar => 29,   // GM 29: Overdriven Guitar
                GuitarTone.ElectricClean => 27,     // GM 27: Electric Guitar Clean
                GuitarTone.AcousticSteel => 25,     // GM 25: Acoustic Guitar Steel
                _ => 29
            };

            // MIDI Program Change (0xC0)
            _synth.ProcessMidiMessage(0, 0xC0, program, 0);
        }

        public void NoteOn(int channel, int key, int velocity = 100)
        {
            if (_synth != null)
            {
                _synth.NoteOn(channel, key, velocity);
            }
            else
            {
                // Fallback procedural guitar voice
                lock (_proceduralVoices)
                {
                    float freq = 440f * MathF.Pow(2f, (key - 69f) / 12f);
                    _proceduralVoices.Add(new ProceduralVoice
                    {
                        Key = key,
                        Frequency = freq,
                        Phase = 0f,
                        Amplitude = (velocity / 127f) * 0.9f,
                        Decay = 0.9997f,
                        Harmonics = CurrentTone == GuitarTone.DistortionGuitar ? 3.5f :
                                    CurrentTone == GuitarTone.OverdriveGuitar ? 2.2f : 1.2f,
                        Active = true
                    });
                }
            }
        }

        public void NoteOff(int channel, int key)
        {
            if (_synth != null)
            {
                _synth.NoteOff(channel, key);
            }
            else
            {
                lock (_proceduralVoices)
                {
                    foreach (var v in _proceduralVoices)
                    {
                        if (v.Key == key) v.Decay = 0.995f; // faster release
                    }
                }
            }
        }

        public void PlayChord(IEnumerable<int> pitches, int velocity = 100)
        {
            foreach (var pitch in pitches)
            {
                NoteOn(0, pitch, velocity);
            }
        }

        public void StopAllNotes()
        {
            _synth?.NoteOffAll(false);
            lock (_proceduralVoices)
            {
                _proceduralVoices.Clear();
            }
        }

        public void SetPitchBend(int channel, float semitones)
        {
            WhammyBendSemitones = semitones;
            if (_synth != null)
            {
                int bendVal = (int)(8192 + (semitones / 2.0f) * 8192);
                bendVal = Math.Clamp(bendVal, 0, 16383);
                int lsb = bendVal & 0x7F;
                int msb = (bendVal >> 7) & 0x7F;
                _synth.ProcessMidiMessage(channel, 0xE0, lsb, msb);
            }
        }

        public void Update()
        {
            if (_dynamicSound != null)
            {
                try
                {
                    while (_dynamicSound.PendingBufferCount < 3)
                    {
                        StreamAudioChunk();
                    }
                    if (_dynamicSound.State != SoundState.Playing)
                    {
                        _dynamicSound.Play();
                    }
                }
                catch { }
            }
        }

        private void StreamAudioChunk()
        {
            if (_dynamicSound == null) return;

            try
            {
                if (_synth != null)
                {
                    _synth.Render(_leftBuffer, _rightBuffer);
                }
                else
                {
                    // Render procedural guitar engine with Marshall 4x12 saturation
                    Array.Clear(_leftBuffer, 0, BufferLength);
                    Array.Clear(_rightBuffer, 0, BufferLength);

                    float bendFactor = MathF.Pow(2f, WhammyBendSemitones / 12f);

                    lock (_proceduralVoices)
                    {
                        for (int i = 0; i < BufferLength; i++)
                        {
                            float sample = 0f;
                            for (int v = 0; v < _proceduralVoices.Count; v++)
                            {
                                var voice = _proceduralVoices[v];
                                if (!voice.Active) continue;

                                float f = voice.Frequency * bendFactor;
                                voice.Phase += f / SampleRate;
                                if (voice.Phase >= 1f) voice.Phase -= 1f;

                                // Multitimbral guitar waveform (fundamental + 2nd/3rd harmonics)
                                float s = MathF.Sin(voice.Phase * MathF.PI * 2f) +
                                          0.5f * MathF.Sin(voice.Phase * MathF.PI * 4f) +
                                          0.25f * MathF.Sin(voice.Phase * MathF.PI * 6f);

                                // Tube saturation
                                s = MathF.Tanh(s * voice.Harmonics);
                                sample += s * voice.Amplitude;

                                voice.Amplitude *= voice.Decay;
                                if (voice.Amplitude < 0.001f) voice.Active = false;
                            }

                            _leftBuffer[i] = sample;
                            _rightBuffer[i] = sample;
                        }

                        _proceduralVoices.RemoveAll(v => !v.Active);
                    }
                }

                int byteIdx = 0;
                for (int i = 0; i < BufferLength; i++)
                {
                    short left = (short)Math.Clamp(_leftBuffer[i] * MasterVolume * 32767f, -32768f, 32767f);
                    short right = (short)Math.Clamp(_rightBuffer[i] * MasterVolume * 32767f, -32768f, 32767f);

                    _pcmBuffer[byteIdx++] = (byte)(left & 0xFF);
                    _pcmBuffer[byteIdx++] = (byte)((left >> 8) & 0xFF);
                    _pcmBuffer[byteIdx++] = (byte)(right & 0xFF);
                    _pcmBuffer[byteIdx++] = (byte)((right >> 8) & 0xFF);
                }

                _dynamicSound.SubmitBuffer(_pcmBuffer);
            }
            catch { }
        }

        public void Dispose()
        {
            _dynamicSound?.Stop();
            _dynamicSound?.Dispose();
            _dynamicSound = null;
            _synth = null;
            _soundFont = null;
        }
    }
}
