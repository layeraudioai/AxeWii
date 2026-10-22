# AxeWii MonoGame / C# Studio

Native cross-platform C# / MonoGame desktop port of the **AxeWii Guitar Hero 3 MIDI & Spatial Synthesizer Studio**.

## Architecture & Subsystems

The port translates every core subsystem from the web application into clean, performant, zero-dependency C# .NET 8 modules:

```
ports/AxeWii.MonoGame/
├── Core/
│   ├── Types.cs                 # Data models, fret states, spatial motion, tuning types, grading
│   ├── NativeFileDialog.cs      # Native desktop OS file dialogs for importing .mid and .sf2
│   └── FileLibraryManager.cs    # Scans Songs/ and SoundFonts/ folders with in-game selection
├── Hardware/
│   └── WiiGuitarHidManager.cs   # Direct Bluetooth WebHID Wiimote communication (no dongle needed)
├── Motion/
│   └── MotionProcessor.cs       # Accelerometer pitch/roll math, 5 octave tilt zones, tare calibration
├── Tuning/
│   ├── MusicTheory.cs           # Standard E, Drop D, Pentatonic, Blues, Synth Bass, Microtonal presets
│   └── FingeringManager.cs      # 32 fret combinations, thumbstick microtuning (cents/transpose/chords)
├── Audio/
│   └── SoundFontSynthEngine.cs  # MeltySynth SF2/GM synth with real-time DynamicSoundEffectInstance PCM
├── Graphics/
│   ├── HighwayRenderer3D.cs     # 3D downward rolling highway, beat lines, note gems, sustain ribbons
│   └── GuitarVisualizer3D.cs    # 3D guitar body & neck physically tilting with Wiimote pitch and roll
├── Midi/
│   ├── SmartTranscriptionMapper.cs # Complete octave-to-tilt-zone and fret fingering auto-mapper
│   └── MidiFileParser.cs        # Binary .mid reader and procedural rock/blues demo generator
├── UI/
│   ├── VectorFontRenderer.cs    # Dependency-free procedural bitmap font renderer (no XNB required)
│   └── HudRenderer.cs           # 5-panel studio UI with SoundFont preset & MIDI file browsers
├── AxeWiiGame.cs                # Main MonoGame game loop tying all engines together
└── Program.cs                   # Application entry point
```

## Running the Game

In `ports/AxeWii.MonoGame`:

```bash
dotnet run
```

Or build with:

```bash
dotnet build
```

---

## SoundFont (.sf2) & MIDI (.mid) Importing

### 1. Interactive Native File Dialogs
- **Press `[O]` or `[I]` anywhere in-game**: Opens the native Windows File Picker dialog. Select any `.mid` or `.sf2` file on your drive to immediately load and synthesize it!
- **Press `[S]` anywhere in-game**: Opens the SoundFont Picker dialog directly for `.sf2` banks.

### 2. Dedicated Library Folders (`./Songs` and `./SoundFonts`)
- You can place your `.mid` / `.midi` files directly into the `Songs/` folder.
- You can place your `.sf2` soundbanks directly into the `SoundFonts/` folder.
- **Press `[R]` in-game** to refresh and discover newly added files immediately.

### 3. In-Game MIDI Song Browser (`F4` Tab)
- Lists all discovered songs in `Songs/` or the app root folder.
- Use **`[Up]` / `[Down]` arrows** to highlight a song and press **`[Enter]`** to load it!
- The Smart Auto-Mapper automatically transcribes the song onto the 3D rhythm highway with 100% playable notes across all octaves.
- Press **`[M]`** anytime to toggle the built-in procedural demo songs (*Guitar Hero 3 Tribute* and *AxeWii Blues Odyssey*).

### 4. SoundFont Preset Browser (`F2` Tab)
- Displays the loaded SoundFont name, path, and preset count.
- Press **`[ [ ]`** and **`[ ] ]`** to cycle through all presets in your SoundFont!
- If no `.sf2` file is provided, AxeWii runs its built-in procedural guitar synthesizer with Marshall 4x12 cabinet simulation, so notes always sound rich and loud!
- Press **`[1]` - `[4]`** in the `F2` tab to switch between the 4 General MIDI core models:
  1. Distortion Guitar (GM 30)
  2. Overdrive Guitar (GM 29)
  3. Clean Electric (GM 27)
  4. Acoustic Steel (GM 25)

---

## Controls

### Hardware (Wii Guitar)
- **Frets (Green, Red, Yellow, Blue, Orange)**: Hold fret combinations
- **Strum Up / Down**: Trigger note/chord strike
- **Open Strum**: Strum without frets held
- **Whammy Bar**: Pitch dive bomb
- **Thumbstick Y**: Nudge fine cents / semitone transpose
- **Thumbstick X**: Cycle chord modifiers (Single, Power 5th, Triad, 7th, Sus4)
- **Minus Button (-)**: Tare / calibrate neck playing stance
- **Plus Button (+)**: Reset fine tunings to defaults

### Keyboard (Testing & Direct Play)
- **F1 - F5**: Switch Studio Tabs (Highway, Tunings & SF2, Motion, MIDI Songs, Hardware)
- **O / I**: Open File Dialog to import MIDI or SF2
- **S**: Open File Dialog to import SoundFont (.sf2)
- **R**: Refresh `Songs/` and `SoundFonts/` library
- **[ / ]**: Previous / Next SoundFont preset
- **1, 2, 3, 4, 5**: Hold Green, Red, Yellow, Blue, Orange frets
- **Tab**: Open Note Strum
- **Space**: Strum Down
- **M**: Switch Built-in Demo Songs (Rock Anthem <-> Blues Odyssey)
- **T**: Send Test Rumble Pulse to Wiimote
- **P**: Toggle WebHID Preset (`UserCustom` <-> `StandardLesPaul`)
- **Esc**: Exit
