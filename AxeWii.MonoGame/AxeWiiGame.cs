using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AxeWii.Core;
using AxeWii.Hardware;
using AxeWii.Graphics;
using AxeWii.Audio;
using AxeWii.Tuning;
using AxeWii.Motion;
using AxeWii.Midi;
using AxeWii.UI;

namespace AxeWii
{
    /// <summary>
    /// AxeWii: Guitar Hero 3 MIDI & Spatial Synthesizer Studio.
    /// Complete C# / MonoGame Desktop Port.
    /// Features:
    /// - Continuous real-time audio playback through DynamicSoundEffectInstance
    /// - Live backing song audio rendering as notes roll down the highway
    /// - 3D downward rolling rhythm highway with unculled depth rendering
    /// - Direct keyboard testing (A/S/D/F/G or 1-5 direct strike, Space strum, Tab open)
    /// - Direct Bluetooth Wiimote WebHID (no dongle needed)
    /// - MeltySynth SoundFont 2 (.sf2) & General MIDI multi-instrument synthesis
    /// - Native desktop file import dialogs for .sf2 and .mid
    /// </summary>
    public class AxeWiiGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;

        // Core Subsystems
        private WiiGuitarHidManager _guitar = null!;
        private SoundFontSynthEngine _synth = null!;
        private FingeringManager _fingeringManager = null!;
        private MotionProcessor _motionProcessor = null!;
        private HighwayRenderer3D _highway = null!;
        private GuitarVisualizer3D _guitarVisualizer = null!;
        private HudRenderer _hud = null!;
        private FileLibraryManager _fileLib = null!;

        // Gameplay & Performance State
        private ParsedMidiSong _currentSong = null!;
        private PerformanceGrade _grade = new();
        private string _recentJudgment = "";
        private float _judgmentTimer = 0f;
        private string _actionNotification = "";
        private float _notificationTimer = 0f;
        private bool _playBackingTrack = true;
        private bool _isPaused = true;            // Paused by default as requested!
        private bool _isLeftHanded = false;        // Default: Right-Handed (Green on Left)
        private KeyboardState _prevKeyboardState;

        public AxeWiiGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720,
                SynchronizeWithVerticalRetrace = true
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
        }

        protected override void Initialize()
        {
            _guitar = new WiiGuitarHidManager();
            _synth = new SoundFontSynthEngine();
            _fingeringManager = new FingeringManager();
            _motionProcessor = new MotionProcessor();
            _highway = new HighwayRenderer3D(GraphicsDevice)
            {
                IsLeftHanded = _isLeftHanded
            };
            _guitarVisualizer = new GuitarVisualizer3D(GraphicsDevice);
            _hud = new HudRenderer(GraphicsDevice);
            _fileLib = new FileLibraryManager();

            // Connect to direct Bluetooth Wiimote (no dongle needed)
            bool connected = _guitar.Connect();
            Console.WriteLine(connected ? "[AxeWii] Connected to Wiimote Bluetooth!" : "[AxeWii] Searching for Bluetooth Wiimote (Press 1+2)...");

            // Hardware event subscriptions
            _guitar.OnStrumHit += OnPlayerStrum;
            _guitar.OnTareMotion += () =>
            {
                _motionProcessor.Tare();
                ShowNotification("Neck Stance Tared / Calibrated (Minus Button)");
            };
            _guitar.OnResetTunings += () =>
            {
                _fingeringManager.ResetAllFineTunings();
                ShowNotification("Fine Tunings Reset to Defaults (Plus Button)");
            };

            // Initial Song loading: check if any discovered .mid exists in ./Songs or root
            string? firstMidi = _fileLib.GetCurrentMidiFile();
            ParsedMidiSong? loaded = null;
            if (firstMidi != null)
            {
                loaded = MidiFileParser.ParseMidiFile(firstMidi);
            }

            LoadSong(loaded ?? MidiFileParser.CreateDemoSong("rock"));

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _synth.Initialize();

            // Check if any .sf2 is found in SoundFonts/ or root
            string? firstSf = _fileLib.GetCurrentSoundFontFile();
            if (firstSf != null)
            {
                _synth.LoadSoundFont(firstSf);
            }
        }

        public void LoadSong(ParsedMidiSong song)
        {
            _currentSong = song;
            _highway.CurrentSongTime = 0f;
            _highway.Bpm = song.Bpm;
            _isPaused = true; // Song paused by default

            foreach (var note in _currentSong.AllEvents)
            {
                note.Hit = false;
                note.PlayedAudio = false;
                note.StoppedAudio = false;
            }

            _grade = new PerformanceGrade
            {
                TotalNotes = song.AllEvents.Count
            };
            ShowNotification($"Loaded Song: {song.Title} (PAUSED - Press [P] or [SPACE] to Play)");
        }

        public void PromptImportFile()
        {
            string? path = NativeFileDialog.OpenAudioOrMidiDialog();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".sf2")
            {
                if (_synth.LoadSoundFont(path))
                {
                    _fileLib.RefreshLibrary();
                    ShowNotification($"Imported SoundFont: {Path.GetFileName(path)} ({_synth.PresetNames.Count} Presets)");
                }
            }
            else if (ext == ".mid" || ext == ".midi")
            {
                var song = MidiFileParser.ParseMidiFile(path);
                if (song != null)
                {
                    _fileLib.RefreshLibrary();
                    LoadSong(song);
                }
                else
                {
                    ShowNotification("Failed to parse MIDI file format.");
                }
            }
        }

        public void PromptImportSoundFont()
        {
            string? path = NativeFileDialog.OpenSoundFontDialog();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            if (_synth.LoadSoundFont(path))
            {
                _fileLib.RefreshLibrary();
                ShowNotification($"Imported SoundFont: {Path.GetFileName(path)} ({_synth.PresetNames.Count} Presets)");
            }
        }

        public void PromptImportMidi()
        {
            string? path = NativeFileDialog.OpenMidiDialog();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            var song = MidiFileParser.ParseMidiFile(path);
            if (song != null)
            {
                _fileLib.RefreshLibrary();
                LoadSong(song);
            }
            else
            {
                ShowNotification("Failed to parse MIDI file format.");
            }
        }

        private void HitFretDirect(string fret)
        {
            _guitar.State.Frets.Green = fret.Equals("green", StringComparison.OrdinalIgnoreCase);
            _guitar.State.Frets.Red = fret.Equals("red", StringComparison.OrdinalIgnoreCase);
            _guitar.State.Frets.Yellow = fret.Equals("yellow", StringComparison.OrdinalIgnoreCase);
            _guitar.State.Frets.Blue = fret.Equals("blue", StringComparison.OrdinalIgnoreCase);
            _guitar.State.Frets.Orange = fret.Equals("orange", StringComparison.OrdinalIgnoreCase);
            _guitar.State.Frets.Open = fret.Equals("open", StringComparison.OrdinalIgnoreCase);

            OnPlayerStrum("DOWN");
        }

        private void OnPlayerStrum(string direction)
        {
            var frets = _guitar.State.Frets;
            string fingeringId = FingeringManager.GetFingeringId(frets);
            int octaveOffset = _motionProcessor.State.OctaveOffset;

            // Get all sounding chord pitches for this held combination
            var (pitches, fineCents, chordName) = _fingeringManager.GetChordPitches(fingeringId, octaveOffset);

            // Play chord via low-latency SoundFont synth on player channel 0
            _synth.PlayChord(pitches, 115);

            // Check hit against highway notes (±140ms window)
            float hitWindow = 0.14f;
            bool hitAny = false;

            string primaryFret = frets.Green ? "green" :
                                frets.Red ? "red" :
                                frets.Yellow ? "yellow" :
                                frets.Blue ? "blue" :
                                frets.Orange ? "orange" : "open";

            foreach (var note in _currentSong.AllEvents)
            {
                if (!note.Hit && Math.Abs(note.Time - _highway.CurrentSongTime) <= hitWindow)
                {
                    if (note.Fret.Equals(primaryFret, StringComparison.OrdinalIgnoreCase))
                    {
                        note.Hit = true;
                        hitAny = true;
                        float delta = Math.Abs(note.Time - _highway.CurrentSongTime);

                        if (delta < 0.045f)
                        {
                            _recentJudgment = "PERFECT!";
                            _grade.PerfectCount++;
                            _grade.Score += 150 * _grade.Multiplier;
                        }
                        else if (delta < 0.09f)
                        {
                            _recentJudgment = "GREAT!";
                            _grade.GreatCount++;
                            _grade.Score += 100 * _grade.Multiplier;
                        }
                        else
                        {
                            _recentJudgment = "GOOD";
                            _grade.GoodCount++;
                            _grade.Score += 50 * _grade.Multiplier;
                        }

                        _grade.NotesHit++;
                        _grade.CurrentStreak++;
                        _grade.MaxCombo = Math.Max(_grade.MaxCombo, _grade.CurrentStreak);
                        _grade.Multiplier = Math.Min(4, 1 + _grade.CurrentStreak / 10);
                        _judgmentTimer = 0.9f;
                        break;
                    }
                }
            }

            if (!hitAny && _currentSong.AllEvents.Exists(n => !n.Hit && Math.Abs(n.Time - _highway.CurrentSongTime) <= hitWindow))
            {
                _recentJudgment = "MISS";
                _grade.MissCount++;
                _grade.CurrentStreak = 0;
                _grade.Multiplier = 1;
                _judgmentTimer = 0.9f;
            }
        }

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var kb = Keyboard.GetState();

            if (kb.IsKeyDown(Keys.Escape))
                Exit();

            // 1. Advance song highway time if not paused
            if (!_isPaused)
            {
                _highway.CurrentSongTime += dt;
                if (_highway.CurrentSongTime > _currentSong.Duration)
                {
                    _highway.CurrentSongTime = 0f;
                    foreach (var note in _currentSong.AllEvents)
                    {
                        note.Hit = false;
                        note.PlayedAudio = false;
                        note.StoppedAudio = false;
                    }
                }

                // 2. Play backing song audio notes in real-time as playhead reaches them
                if (_playBackingTrack)
                {
                    foreach (var note in _currentSong.AllEvents)
                    {
                        if (!note.PlayedAudio && _highway.CurrentSongTime >= note.Time && _highway.CurrentSongTime < note.Time + 0.25f)
                        {
                            note.PlayedAudio = true;
                            _synth.NoteOn(1, note.Pitch, (int)(note.Velocity * 0.85f));
                        }
                        if (note.PlayedAudio && !note.StoppedAudio && _highway.CurrentSongTime >= note.Time + Math.Max(0.12f, note.Duration))
                        {
                            note.StoppedAudio = true;
                            _synth.NoteOff(1, note.Pitch);
                        }
                    }
                }
            }

            // 3. Continuously stream and refill audio buffers
            _synth.Update();

            // 4. Process Wiimote 3D Motion (Pitch elevation & roll)
            _motionProcessor.Process(_guitar.State.AccelX, _guitar.State.AccelY, _guitar.State.AccelZ);

            // 5. Process Analog Thumbstick Live Fine-Tuning & Chord Modifier Cycling
            if (Math.Abs(_guitar.State.JoystickX) > 0.25f || Math.Abs(_guitar.State.JoystickY) > 0.25f)
            {
                string heldId = FingeringManager.GetFingeringId(_guitar.State.Frets);
                if (_fingeringManager.HandleStickInput(heldId, _guitar.State.JoystickX, _guitar.State.JoystickY, out string msg))
                {
                    ShowNotification(msg);
                }
            }

            // 6. Apply Whammy Bar Dive Bomb
            _synth.WhammyBendSemitones = -_guitar.State.Whammy * 5.0f;
            _synth.SetPitchBend(0, _synth.WhammyBendSemitones);

            // 7. Handle Keyboard Controls & Tab Switching
            HandleKeyboardInput(kb);

            // 8. Timers
            if (_judgmentTimer > 0f)
            {
                _judgmentTimer -= dt;
                if (_judgmentTimer <= 0f) _recentJudgment = "";
            }

            if (_notificationTimer > 0f)
            {
                _notificationTimer -= dt;
                if (_notificationTimer <= 0f) _actionNotification = "";
            }

            // 9. Update Window Title Telemetry
            string conn = _guitar.State.Connected ? $"[BT Connected: {_guitar.State.DeviceName}]" : "[Searching BT (Press 1+2)]";
            string judge = string.IsNullOrEmpty(_recentJudgment) ? "" : $" | {_recentJudgment}";
            string pauseTag = _isPaused ? " [PAUSED]" : " [PLAYING]";
            string handTag = _isLeftHanded ? " [Lefty Flip]" : " [Right-Handed]";
            Window.Title = $"AxeWii Studio - {_currentSong.Title}{pauseTag}{handTag} - Score: {_grade.Score:N0} ({_grade.Multiplier}X) Acc: {_grade.Accuracy:F1}%{judge} | {conn}";

            _prevKeyboardState = kb;
            base.Update(gameTime);
        }

        private void HandleKeyboardInput(KeyboardState kb)
        {
            // Tab Switchers F1 - F5
            if (IsKeyPressed(kb, Keys.F1)) _hud.ActiveTab = ViewTab.Highway;
            if (IsKeyPressed(kb, Keys.F2)) _hud.ActiveTab = ViewTab.FretTuning;
            if (IsKeyPressed(kb, Keys.F3)) _hud.ActiveTab = ViewTab.Motion;
            if (IsKeyPressed(kb, Keys.F4)) _hud.ActiveTab = ViewTab.MidiStudio;
            if (IsKeyPressed(kb, Keys.F5)) _hud.ActiveTab = ViewTab.Diagnostics;

            // P: Play / Pause Toggle
            if (IsKeyPressed(kb, Keys.P))
            {
                _isPaused = !_isPaused;
                if (_isPaused) _synth.StopAllNotes();
                ShowNotification(_isPaused ? "Song PAUSED (Press [P] or [SPACE] to Resume)" : "Song PLAYING");
            }

            // L: Left-Handed / Right-Handed Mode Swap
            if (IsKeyPressed(kb, Keys.L))
            {
                _isLeftHanded = !_isLeftHanded;
                _highway.IsLeftHanded = _isLeftHanded;
                ShowNotification(_isLeftHanded 
                    ? "Handedness: LEFT-HANDED / LEFTY FLIP (Green on Right)" 
                    : "Handedness: RIGHT-HANDED (Green on Left)");
            }

            // Home / Backspace: Rewind Song to Beginning
            if (IsKeyPressed(kb, Keys.Home) || IsKeyPressed(kb, Keys.Back))
            {
                _highway.CurrentSongTime = 0f;
                _synth.StopAllNotes();
                foreach (var note in _currentSong.AllEvents)
                {
                    note.Hit = false;
                    note.PlayedAudio = false;
                    note.StoppedAudio = false;
                }
                _grade = new PerformanceGrade { TotalNotes = _currentSong.AllEvents.Count };
                ShowNotification("Song Rewound to Beginning (Paused)");
            }

            // Import Hotkeys: O / I -> Universal Import; S -> SoundFont Import
            if (IsKeyPressed(kb, Keys.O) || IsKeyPressed(kb, Keys.I))
            {
                PromptImportFile();
            }

            if (IsKeyPressed(kb, Keys.S) && _hud.ActiveTab == ViewTab.FretTuning)
            {
                PromptImportSoundFont();
            }

            // B: Toggle backing track audio
            if (IsKeyPressed(kb, Keys.B))
            {
                _playBackingTrack = !_playBackingTrack;
                if (!_playBackingTrack) _synth.StopAllNotes();
                ShowNotification(_playBackingTrack ? "Song Backing Track: ON" : "Song Backing Track: MUTED (Strum-only Mode)");
            }

            // R: Refresh Library
            if (IsKeyPressed(kb, Keys.R))
            {
                _fileLib.RefreshLibrary();
                ShowNotification($"Refreshed Library: {_fileLib.DiscoveredMidiFiles.Count} Songs, {_fileLib.DiscoveredSoundFontFiles.Count} SoundFonts");
            }

            // Preset Cycling for SoundFont: [ and ]
            if (IsKeyPressed(kb, Keys.OemOpenBrackets))
            {
                _synth.CyclePreset(-1);
                ShowNotification($"SoundFont Preset: {_synth.CurrentPresetName}");
            }

            if (IsKeyPressed(kb, Keys.OemCloseBrackets))
            {
                _synth.CyclePreset(1);
                ShowNotification($"SoundFont Preset: {_synth.CurrentPresetName}");
            }

            // In F4 (MIDI Studio): Up/Down navigate discovered songs, Enter loads selected
            if (_hud.ActiveTab == ViewTab.MidiStudio && _fileLib.DiscoveredMidiFiles.Count > 0)
            {
                if (IsKeyPressed(kb, Keys.Up))
                {
                    _fileLib.SelectedMidiIndex = (_fileLib.SelectedMidiIndex - 1 + _fileLib.DiscoveredMidiFiles.Count) % _fileLib.DiscoveredMidiFiles.Count;
                }
                if (IsKeyPressed(kb, Keys.Down))
                {
                    _fileLib.SelectedMidiIndex = (_fileLib.SelectedMidiIndex + 1) % _fileLib.DiscoveredMidiFiles.Count;
                }
                if (IsKeyPressed(kb, Keys.Enter))
                {
                    string? chosen = _fileLib.GetCurrentMidiFile();
                    if (chosen != null)
                    {
                        var parsed = MidiFileParser.ParseMidiFile(chosen);
                        if (parsed != null) LoadSong(parsed);
                    }
                }
            }

            // In F2 (Tunings & SF2): Keys 1-4 switch GM Tones
            if (_hud.ActiveTab == ViewTab.FretTuning)
            {
                if (IsKeyPressed(kb, Keys.D1)) { _synth.SetTone(GuitarTone.DistortionGuitar); ShowNotification("Tone: Distortion Guitar (GM 30)"); }
                if (IsKeyPressed(kb, Keys.D2)) { _synth.SetTone(GuitarTone.OverdriveGuitar); ShowNotification("Tone: Overdrive Guitar (GM 29)"); }
                if (IsKeyPressed(kb, Keys.D3)) { _synth.SetTone(GuitarTone.ElectricClean); ShowNotification("Tone: Electric Clean (GM 27)"); }
                if (IsKeyPressed(kb, Keys.D4)) { _synth.SetTone(GuitarTone.AcousticSteel); ShowNotification("Tone: Acoustic Steel (GM 25)"); }

                // NumPad 1-6 -> Apply Tuning Presets
                Keys[] presetKeys = { Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5, Keys.NumPad6 };
                for (int i = 0; i < presetKeys.Length && i < MusicTheory.TuningPresets.Count; i++)
                {
                    if (IsKeyPressed(kb, presetKeys[i]))
                    {
                        var preset = MusicTheory.TuningPresets[i];
                        _fingeringManager.ApplyPreset(preset);
                        ShowNotification($"Applied Tuning Preset: {preset.Name}");
                    }
                }
            }

            // Direct keyboard fret hit & strum (A=Green, S=Red, D=Yellow, F=Blue, G=Orange)
            if (IsKeyPressed(kb, Keys.A)) HitFretDirect("green");
            if (IsKeyPressed(kb, Keys.S) && _hud.ActiveTab != ViewTab.FretTuning) HitFretDirect("red");
            if (IsKeyPressed(kb, Keys.D)) HitFretDirect("yellow");
            if (IsKeyPressed(kb, Keys.F)) HitFretDirect("blue");
            if (IsKeyPressed(kb, Keys.G)) HitFretDirect("orange");

            // Direct keyboard hit via number keys when on Highway tab
            if (_hud.ActiveTab == ViewTab.Highway)
            {
                if (IsKeyPressed(kb, Keys.D1)) HitFretDirect("green");
                if (IsKeyPressed(kb, Keys.D2)) HitFretDirect("red");
                if (IsKeyPressed(kb, Keys.D3)) HitFretDirect("yellow");
                if (IsKeyPressed(kb, Keys.D4)) HitFretDirect("blue");
                if (IsKeyPressed(kb, Keys.D5)) HitFretDirect("orange");
            }

            // Space: If paused and no frets held, unpauses; otherwise Strum Down with currently held frets
            if (IsKeyPressed(kb, Keys.Space))
            {
                bool anyFret = _guitar.State.Frets.Green || _guitar.State.Frets.Red || 
                               _guitar.State.Frets.Yellow || _guitar.State.Frets.Blue || 
                               _guitar.State.Frets.Orange;

                if (_isPaused && !anyFret)
                {
                    _isPaused = false;
                    ShowNotification("Song PLAYING (Press [P] to Pause)");
                }
                else
                {
                    OnPlayerStrum("DOWN");
                }
            }

            // Tab: Open Strum
            if (IsKeyPressed(kb, Keys.Tab))
            {
                HitFretDirect("open");
            }

            // M: Toggle Built-in Demo Songs
            if (IsKeyPressed(kb, Keys.M))
            {
                string nextSong = _currentSong.Title.Contains("Rock") ? "blues" : "rock";
                LoadSong(MidiFileParser.CreateDemoSong(nextSong));
            }

            // T: Test Rumble
            if (IsKeyPressed(kb, Keys.T))
            {
                _guitar.TestRumble();
                ShowNotification("Sent 250ms Test Rumble Pulse to Wiimote");
            }

            // C: Toggle WebHID Preset (UserCustom <-> StandardLesPaul)
            if (IsKeyPressed(kb, Keys.C))
            {
                _guitar.Preset = _guitar.Preset == WebHidPreset.UserCustom ? WebHidPreset.StandardLesPaul : WebHidPreset.UserCustom;
                ShowNotification($"WebHID Preset Changed to: {_guitar.Preset}");
            }
        }

        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _prevKeyboardState.IsKeyUp(key);
        }

        private void ShowNotification(string text)
        {
            _actionNotification = text;
            _notificationTimer = 3.5f;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(9, 11, 16));

            int screenWidth = GraphicsDevice.Viewport.Width;
            int screenHeight = GraphicsDevice.Viewport.Height;

            // 1. Draw 3D Rhythm Highway (Notes rolling DOWN towards player)
            if (_hud.ActiveTab == ViewTab.Highway)
            {
                // Ensure clean 3D states unaffected by SpriteBatch
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;
                GraphicsDevice.BlendState = BlendState.Opaque;

                _highway.Draw(_spriteBatch, _guitar.State, _currentSong.AllEvents);

                // 2. Draw 3D Guitar Body & Neck Visualizer tilting with real Wiimote pitch and roll (mirrors if left-handed)
                _guitarVisualizer.Draw(_guitar.State, _motionProcessor.State, _isLeftHanded);
            }

            // 3. Draw 2D Comprehensive HUD & Active Panel
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _hud.Draw(
                _spriteBatch,
                screenWidth,
                screenHeight,
                _guitar.State,
                _motionProcessor.State,
                _fingeringManager,
                _synth,
                _fileLib,
                _grade,
                _currentSong.Title,
                _highway.CurrentSongTime,
                _currentSong.Duration,
                _highway.Bpm,
                _recentJudgment,
                _actionNotification,
                _isPaused,
                _isLeftHanded
            );
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            _guitar.Dispose();
            _synth.Dispose();
            base.UnloadContent();
        }
    }
}
