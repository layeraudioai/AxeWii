using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AxeWii.Core;
using AxeWii.Hardware;
using AxeWii.Motion;
using AxeWii.Tuning;
using AxeWii.Audio;

namespace AxeWii.UI
{
    public enum ViewTab
    {
        Highway,     // F1: 3D Rhythm Highway & Gameplay
        FretTuning,  // F2: Fret Tuning & SoundFont 2 Studio
        Motion,      // F3: 3D Spatial Motion & Neck Tilt Zones
        MidiStudio,  // F4: MIDI Song Studio & File Library
        Diagnostics  // F5: Hardware Inspector & Raw Bits
    }

    /// <summary>
    /// Comprehensive HUD and Multi-Panel Interface Renderer for MonoGame.
    /// Provides full on-screen UI matching all panels from the AxeWii web application,
    /// including interactive SoundFont 2 (.sf2) management and MIDI (.mid) file library importing.
    /// </summary>
    public class HudRenderer
    {
        private readonly VectorFontRenderer _font;

        public ViewTab ActiveTab { get; set; } = ViewTab.Highway;

        private static readonly Color HeaderBg = new Color(15, 23, 42);
        private static readonly Color CardBg = new Color(15, 23, 42, 220);
        private static readonly Color CardBgDarker = new Color(11, 17, 32, 230);
        private static readonly Color BorderColor = new Color(51, 65, 85);
        private static readonly Color AccentCyan = new Color(6, 182, 212);
        private static readonly Color AccentPurple = new Color(168, 85, 247);
        private static readonly Color AccentGreen = new Color(34, 197, 94);
        private static readonly Color AccentGold = new Color(234, 179, 8);
        private static readonly Color AccentRed = new Color(239, 68, 68);

        public HudRenderer(GraphicsDevice graphicsDevice)
        {
            _font = new VectorFontRenderer(graphicsDevice);
        }

        public void Draw(
            SpriteBatch spriteBatch,
            int screenWidth,
            int screenHeight,
            GuitarState guitar,
            SpatialMotionState motion,
            FingeringManager fingeringManager,
            SoundFontSynthEngine synth,
            FileLibraryManager fileLib,
            PerformanceGrade grade,
            string currentSongTitle,
            float songTime,
            float songDuration,
            int bpm,
            string recentJudgment,
            string actionNotification,
            bool isPaused,
            bool isLeftHanded)
        {
            // 1. Draw Top Header with Tab Switcher, Handedness & Connection Status
            DrawTopBar(spriteBatch, screenWidth, guitar, actionNotification, isPaused, isLeftHanded);

            // 2. Draw Active Tab Content
            switch (ActiveTab)
            {
                case ViewTab.Highway:
                    DrawHighwayHud(spriteBatch, screenWidth, screenHeight, grade, recentJudgment, currentSongTitle, songTime, songDuration, bpm, isPaused, isLeftHanded);
                    break;
                case ViewTab.FretTuning:
                    DrawFretTuningPanel(spriteBatch, screenWidth, screenHeight, guitar, fingeringManager, synth, fileLib);
                    break;
                case ViewTab.Motion:
                    DrawMotionPanel(spriteBatch, screenWidth, screenHeight, motion);
                    break;
                case ViewTab.MidiStudio:
                    DrawMidiStudioPanel(spriteBatch, screenWidth, screenHeight, currentSongTitle, songTime, songDuration, bpm, fileLib);
                    break;
                case ViewTab.Diagnostics:
                    DrawDiagnosticsPanel(spriteBatch, screenWidth, screenHeight, guitar);
                    break;
            }

            // 3. Draw Bottom Quick-Help Hotkey Bar
            DrawBottomBar(spriteBatch, screenWidth, screenHeight);
        }

        private void DrawTopBar(SpriteBatch sb, int width, GuitarState guitar, string notification, bool isPaused, bool isLeftHanded)
        {
            _font.DrawRectangle(sb, new Rectangle(0, 0, width, 44), HeaderBg);
            _font.DrawBorder(sb, new Rectangle(0, 0, width, 44), BorderColor);

            // Title
            _font.DrawString(sb, "AXEWII", new Vector2(16, 12), AccentCyan, 2.5f);
            _font.DrawString(sb, "STUDIO", new Vector2(98, 14), Color.White, 2.0f);

            // Tabs: F1 to F5
            string[] tabs = { "F1: HIGHWAY", "F2: TUNINGS & SF2", "F3: MOTION", "F4: MIDI SONGS", "F5: HARDWARE" };
            int tabX = 200;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = (int)ActiveTab == i;
                Rectangle tabRect = new Rectangle(tabX, 8, 108, 28);
                if (active)
                {
                    _font.DrawRectangle(sb, tabRect, new Color(30, 41, 59));
                    _font.DrawBorder(sb, tabRect, AccentCyan);
                }
                _font.DrawString(sb, tabs[i], new Vector2(tabX + 6, 14), active ? AccentCyan : Color.Gray, 1.4f);
                tabX += 114;
            }

            // Play/Pause badge & Handedness badge
            string playStatus = isPaused ? "[PAUSED (P)]" : "[PLAYING]";
            Color playCol = isPaused ? AccentGold : AccentGreen;
            _font.DrawString(sb, playStatus, new Vector2(tabX + 10, 14), playCol, 1.6f);

            string handStatus = isLeftHanded ? "[LEFTY (L)]" : "[RIGHTY (L)]";
            _font.DrawString(sb, handStatus, new Vector2(tabX + 115, 14), AccentPurple, 1.6f);

            // Wiimote connection pill on the right
            string connText = guitar.Connected ? "[BT: CONNECTED]" : "[BT: PRESS 1+2]";
            Color connCol = guitar.Connected ? AccentGreen : AccentGold;
            _font.DrawString(sb, connText, new Vector2(width - 170, 14), connCol, 1.6f);

            // Live action notification banner if active
            if (!string.IsNullOrEmpty(notification))
            {
                _font.DrawString(sb, notification, new Vector2(width / 2 - 140, 52), AccentGold, 1.8f);
            }
        }

        private void DrawHighwayHud(
            SpriteBatch sb, int width, int height,
            PerformanceGrade grade, string judgment,
            string songTitle, float time, float dur, int bpm,
            bool isPaused, bool isLeftHanded)
        {
            // Left Card: Score & Multiplier
            Rectangle scoreCard = new Rectangle(20, 60, 240, 140);
            _font.DrawRectangle(sb, scoreCard, CardBg);
            _font.DrawBorder(sb, scoreCard, BorderColor);

            _font.DrawString(sb, "SCORE", new Vector2(35, 75), Color.Gray, 1.6f);
            _font.DrawString(sb, $"{grade.Score:N0}", new Vector2(35, 95), Color.White, 3.0f);

            _font.DrawString(sb, $"MULTIPLIER: {grade.Multiplier}X", new Vector2(35, 140), AccentCyan, 1.8f);
            _font.DrawString(sb, $"COMBO: {grade.CurrentStreak} (MAX: {grade.MaxCombo})", new Vector2(35, 165), AccentGreen, 1.6f);

            // Center: Recent Hit Judgment (PERFECT, GREAT, GOOD, MISS)
            if (!string.IsNullOrEmpty(judgment))
            {
                Color judgeColor = judgment.Contains("PERFECT") ? AccentPurple :
                                   judgment.Contains("GREAT") ? AccentCyan :
                                   judgment.Contains("GOOD") ? AccentGreen : AccentRed;

                _font.DrawString(sb, judgment, new Vector2(width / 2 - 80, height - 160), judgeColor, 3.8f);
            }

            // Right Card: Song Info & Accuracy
            Rectangle infoCard = new Rectangle(width - 260, 60, 240, 140);
            _font.DrawRectangle(sb, infoCard, CardBg);
            _font.DrawBorder(sb, infoCard, BorderColor);

            _font.DrawString(sb, songTitle, new Vector2(width - 245, 75), Color.White, 1.8f);
            _font.DrawString(sb, $"{bpm} BPM | {grade.Accuracy:F1}% ACCURACY", new Vector2(width - 245, 105), AccentGold, 1.6f);
            _font.DrawString(sb, $"GRADE: {grade.LetterGrade}", new Vector2(width - 245, 130), AccentPurple, 2.4f);
            string timeStatus = isPaused ? $"TIME: {time:F1}s / {dur:F1}s [PAUSED]" : $"TIME: {time:F1}s / {dur:F1}s";
            _font.DrawString(sb, timeStatus, new Vector2(width - 245, 165), isPaused ? AccentGold : Color.LightGray, 1.6f);

            // Center Overlay: Prominent Pause & Handedness Card when Paused
            if (isPaused)
            {
                int cardW = 560;
                int cardH = 145;
                Rectangle pauseCard = new Rectangle((width - cardW) / 2, (height - cardH) / 2 - 30, cardW, cardH);
                _font.DrawRectangle(sb, pauseCard, new Color(11, 17, 32, 245));
                _font.DrawBorder(sb, pauseCard, AccentGold, 2);

                _font.DrawString(sb, "PAUSED", new Vector2((width - cardW) / 2 + 225, pauseCard.Y + 16), AccentGold, 3.2f);
                _font.DrawString(sb, "Press [P] or [SPACE] to Play / Resume | [Home] to Rewind", new Vector2((width - cardW) / 2 + 35, pauseCard.Y + 58), Color.White, 1.8f);

                string handDesc = isLeftHanded ? "LEFT-HANDED / LEFTY FLIP (Green on Right)" : "RIGHT-HANDED (Green on Left)";
                Color handColor = isLeftHanded ? AccentPurple : AccentGreen;
                _font.DrawString(sb, $"Press [L] to Swap Handedness: {handDesc}", new Vector2((width - cardW) / 2 + 25, pauseCard.Y + 86), handColor, 1.7f);
                _font.DrawString(sb, "Practice notes anytime: A/S/D/F/G or 1-5 frets, Space to strum", new Vector2((width - cardW) / 2 + 32, pauseCard.Y + 114), Color.LightGray, 1.5f);
            }
        }

        private void DrawFretTuningPanel(
            SpriteBatch sb, int width, int height,
            GuitarState guitar, FingeringManager fm,
            SoundFontSynthEngine synth, FileLibraryManager fileLib)
        {
            Rectangle panel = new Rectangle(50, 65, width - 100, height - 110);
            _font.DrawRectangle(sb, panel, CardBg);
            _font.DrawBorder(sb, panel, BorderColor, 2);

            _font.DrawString(sb, "FRET TUNING & SOUNDFONT 2 (.SF2) STUDIO", new Vector2(75, 80), AccentCyan, 2.5f);

            // Top Card: SoundFont 2 Bank & Preset Explorer
            Rectangle sfCard = new Rectangle(75, 115, width - 150, 105);
            _font.DrawRectangle(sb, sfCard, CardBgDarker);
            _font.DrawBorder(sb, sfCard, AccentPurple);

            string sfStatus = synth.LoadedSoundFontName != null 
                ? $"SOUNDFONT 2: {synth.LoadedSoundFontName} ({synth.PresetNames.Count} PRESETS)"
                : "SOUNDFONT 2: NONE LOADED (USING PROCEDURAL TUBE OVERDRIVE GM MODEL)";
            Color sfCol = synth.LoadedSoundFontName != null ? AccentGreen : AccentGold;

            _font.DrawString(sb, sfStatus, new Vector2(90, 125), sfCol, 2.0f);
            _font.DrawString(sb, $"ACTIVE PRESET: {synth.CurrentPresetName}", new Vector2(90, 150), Color.White, 1.8f);
            _font.DrawString(sb, "HOTKEYS: Press [S] to Import .sf2 File from Disk | Press [ [ ] / [ ] ] to Cycle Presets", new Vector2(90, 175), AccentCyan, 1.6f);
            _font.DrawString(sb, "GM TONES: [1] Distortion (GM 30) | [2] Overdrive (GM 29) | [3] Clean (GM 27) | [4] Acoustic Steel (GM 25)", new Vector2(90, 195), Color.Gray, 1.5f);

            // Middle Card: Current Active Fingering
            string heldId = FingeringManager.GetFingeringId(guitar.Frets);
            var currentTuning = fm.GetTuning(heldId);
            var (pitches, cents, chordName) = fm.GetChordPitches(heldId);

            Rectangle activeCard = new Rectangle(75, 230, width - 150, 80);
            _font.DrawRectangle(sb, activeCard, CardBgDarker);
            _font.DrawBorder(sb, activeCard, AccentCyan);

            _font.DrawString(sb, $"CURRENT FINGERING: {currentTuning.Label}", new Vector2(90, 240), Color.White, 2.2f);
            _font.DrawString(sb, $"ROOT: {currentTuning.RootNote} | CHORD VOICING: {chordName} | CENTS: {(cents >= 0 ? "+" : "")}{cents}¢", new Vector2(90, 268), AccentCyan, 1.8f);
            _font.DrawString(sb, $"SOUNDING MIDI NOTES: [{string.Join(", ", pitches)}]", new Vector2(90, 288), AccentGreen, 1.6f);

            // Bottom Section: Tuning Presets list
            _font.DrawString(sb, "TUNING PRESETS (PRESS NUMPAD 1-6 TO APPLY PRESET):", new Vector2(75, 325), AccentGold, 2.0f);
            int py = 355;
            for (int i = 0; i < MusicTheory.TuningPresets.Count; i++)
            {
                var p = MusicTheory.TuningPresets[i];
                _font.DrawString(sb, $"{i + 1}. {p.Name}", new Vector2(90, py), Color.White, 1.7f);
                _font.DrawString(sb, p.Description, new Vector2(350, py), Color.Gray, 1.5f);
                py += 30;
            }
        }

        private void DrawMotionPanel(SpriteBatch sb, int width, int height, SpatialMotionState motion)
        {
            Rectangle panel = new Rectangle(50, 70, width - 100, height - 130);
            _font.DrawRectangle(sb, panel, CardBg);
            _font.DrawBorder(sb, panel, BorderColor, 2);

            _font.DrawString(sb, "3D SPATIAL MOTION & NECK TILT", new Vector2(75, 95), AccentCyan, 2.6f);
            _font.DrawString(sb, "Tilt the physical Wii Remote neck up/down to shift octaves (-2 to +2). Minus (-) button tares stance.", new Vector2(75, 130), Color.Gray, 1.8f);

            // Pitch & Roll meters
            _font.DrawString(sb, $"NECK PITCH ELEVATION: {motion.Pitch:F1} DEG", new Vector2(75, 180), Color.White, 2.2f);
            _font.DrawString(sb, $"BODY ROLL ROTATION:    {motion.Roll:F1} DEG", new Vector2(75, 215), Color.White, 2.2f);

            // Active Tilt Zone Card
            Rectangle zoneCard = new Rectangle(75, 260, 400, 110);
            _font.DrawRectangle(sb, zoneCard, new Color(30, 41, 59));
            _font.DrawBorder(sb, zoneCard, AccentPurple);

            _font.DrawString(sb, $"ACTIVE TILT ZONE: {motion.ActiveTiltZone.ToString().ToUpper()}", new Vector2(95, 280), AccentPurple, 2.4f);
            _font.DrawString(sb, $"OCTAVE OFFSET: {(motion.OctaveOffset >= 0 ? "+" : "")}{motion.OctaveOffset} OCTAVES", new Vector2(95, 315), AccentCyan, 2.0f);
            _font.DrawString(sb, $"ROLL ZONE: {motion.ActiveRollZone.ToString().ToUpper()}", new Vector2(95, 340), AccentGreen, 1.8f);

            // Accelerometer readout
            _font.DrawString(sb, $"ACCEL X: {motion.AccelX:F2}G | Y: {motion.AccelY:F2}G | Z: {motion.AccelZ:F2}G", new Vector2(75, 410), Color.LightGray, 1.8f);
            _font.DrawString(sb, $"DELTA ACCEL (JERK): {motion.DeltaAccel:F1} G/s {(motion.IsJerkTriggered ? "[ACCENT STRIKE TRIGGERED!]" : "")}", new Vector2(75, 440), motion.IsJerkTriggered ? AccentGold : Color.Gray, 1.8f);
        }

        private void DrawMidiStudioPanel(
            SpriteBatch sb, int width, int height,
            string title, float time, float dur, int bpm,
            FileLibraryManager fileLib)
        {
            Rectangle panel = new Rectangle(50, 65, width - 100, height - 110);
            _font.DrawRectangle(sb, panel, CardBg);
            _font.DrawBorder(sb, panel, BorderColor, 2);

            _font.DrawString(sb, "MIDI SONG STUDIO & FILE LIBRARY", new Vector2(75, 80), AccentCyan, 2.5f);
            _font.DrawString(sb, "Import any standard MIDI file (.mid / .midi). Smart Auto-Mapper transcribes all octaves.", new Vector2(75, 110), Color.Gray, 1.7f);

            // Current Song Header Card
            Rectangle nowCard = new Rectangle(75, 135, width - 150, 80);
            _font.DrawRectangle(sb, nowCard, CardBgDarker);
            _font.DrawBorder(sb, nowCard, AccentCyan);

            _font.DrawString(sb, $"PLAYING: {title}", new Vector2(90, 145), Color.White, 2.2f);
            _font.DrawString(sb, $"TEMPO: {bpm} BPM | DURATION: {dur:F1}s | PLAYHEAD: {time:F1}s", new Vector2(90, 172), AccentGold, 1.8f);

            // Progress Bar
            float progress = Math.Clamp(time / Math.Max(1f, dur), 0f, 1f);
            Rectangle barBg = new Rectangle(90, 195, width - 180, 12);
            Rectangle barFill = new Rectangle(90, 195, (int)((width - 180) * progress), 12);
            _font.DrawRectangle(sb, barBg, new Color(30, 41, 59));
            _font.DrawRectangle(sb, barFill, AccentCyan);

            // Discovered Songs Library Card
            Rectangle libCard = new Rectangle(75, 225, width - 150, 260);
            _font.DrawRectangle(sb, libCard, CardBgDarker);
            _font.DrawBorder(sb, libCard, BorderColor);

            _font.DrawString(sb, "DISCOVERED SONGS (IN ./Songs OR ROOT):", new Vector2(90, 235), AccentPurple, 2.0f);
            _font.DrawString(sb, "HOTKEYS: Press [O] or [I] to Import MIDI from Disk | [Up/Down] Highlight | [Enter] Load | [R] Refresh", new Vector2(90, 260), AccentGold, 1.6f);

            if (fileLib.DiscoveredMidiFiles.Count > 0)
            {
                int my = 290;
                int maxDisplay = Math.Min(fileLib.DiscoveredMidiFiles.Count, 6);
                for (int i = 0; i < maxDisplay; i++)
                {
                    bool selected = i == fileLib.SelectedMidiIndex;
                    string prefix = selected ? "> " : "  ";
                    string name = Path.GetFileName(fileLib.DiscoveredMidiFiles[i]);
                    Color itemCol = selected ? AccentCyan : Color.LightGray;

                    if (selected)
                    {
                        Rectangle selRect = new Rectangle(88, my - 2, width - 176, 24);
                        _font.DrawRectangle(sb, selRect, new Color(30, 41, 59));
                        _font.DrawBorder(sb, selRect, AccentCyan);
                    }

                    _font.DrawString(sb, $"{prefix}{name}", new Vector2(95, my + 2), itemCol, 1.8f);
                    my += 28;
                }
            }
            else
            {
                _font.DrawString(sb, "No .mid files found in ./Songs or application folder.", new Vector2(90, 300), Color.Gray, 1.8f);
                _font.DrawString(sb, "Press [O] to open the file dialog and import any .mid file from your computer!", new Vector2(90, 330), AccentCyan, 1.8f);
            }

            _font.DrawString(sb, "BUILT-IN DEMO SONGS: Press [M] to Toggle Demo Songs (Rock Anthem <-> Blues Odyssey)", new Vector2(90, 455), Color.LightGray, 1.6f);
        }

        private void DrawDiagnosticsPanel(SpriteBatch sb, int width, int height, GuitarState guitar)
        {
            Rectangle panel = new Rectangle(50, 70, width - 100, height - 130);
            _font.DrawRectangle(sb, panel, CardBg);
            _font.DrawBorder(sb, panel, BorderColor, 2);

            _font.DrawString(sb, "HARDWARE INSPECTOR & DIAGNOSTICS", new Vector2(75, 95), AccentCyan, 2.6f);
            _font.DrawString(sb, "Direct Bluetooth WebHID connection (No Dongle or Middleman required).", new Vector2(75, 130), Color.Gray, 1.8f);

            _font.DrawString(sb, $"DEVICE: {guitar.DeviceName}", new Vector2(75, 175), Color.White, 2.0f);
            _font.DrawString(sb, $"STATUS: {(guitar.Connected ? "CONNECTED VIA BLUETOOTH" : "SEARCHING FOR WIIMOTE (PRESS 1+2)")}", new Vector2(75, 205), guitar.Connected ? AccentGreen : AccentGold, 1.8f);

            // Controls status
            _font.DrawString(sb, $"STRUM UP: {guitar.StrumUp} | STRUM DOWN: {guitar.StrumDown}", new Vector2(75, 250), guitar.StrumUp || guitar.StrumDown ? AccentGreen : Color.LightGray, 1.8f);
            _font.DrawString(sb, $"WHAMMY DEFLECTION: {guitar.Whammy:F2} ({(guitar.Whammy * 100f):F0}%)", new Vector2(75, 280), guitar.Whammy > 0.05f ? AccentCyan : Color.LightGray, 1.8f);
            _font.DrawString(sb, $"ANALOG THUMBSTICK: X: {guitar.JoystickX:F2}, Y: {guitar.JoystickY:F2}", new Vector2(75, 310), Color.LightGray, 1.8f);
            _font.DrawString(sb, $"MINUS BUTTON (TARE): {guitar.ButtonMinus} | PLUS BUTTON (RESET): {guitar.ButtonPlus}", new Vector2(75, 340), Color.LightGray, 1.8f);

            // Quick Hardware Test Actions
            _font.DrawString(sb, "HARDWARE TEST HOTKEYS:", new Vector2(75, 395), AccentGold, 2.0f);
            _font.DrawString(sb, "Press 'T' -> Send Test Rumble pulse (250ms motor burst)", new Vector2(95, 425), Color.White, 1.8f);
            _font.DrawString(sb, "Press 'C' -> Toggle WebHID Preset (User Custom <-> Standard Les Paul)", new Vector2(95, 455), Color.White, 1.8f);
            _font.DrawString(sb, "Press 'L' -> Swap Left-Handed / Right-Handed Mode", new Vector2(95, 485), AccentPurple, 1.8f);
        }

        private void DrawBottomBar(SpriteBatch sb, int width, int height)
        {
            _font.DrawRectangle(sb, new Rectangle(0, height - 32, width, 32), HeaderBg);
            _font.DrawBorder(sb, new Rectangle(0, height - 32, width, 32), BorderColor);

            string help = "HOTKEYS: [P] Play/Pause | [L] Left/Righty | [Home] Rewind | [1-5]/[A-G] Frets | [Space] Strum | [O] Import | [M] Songs | [F1-F5] Tabs";
            _font.DrawString(sb, help, new Vector2(16, height - 22), Color.Gray, 1.45f);
        }
    }
}
