using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AxeWii.Core
{
    /// <summary>
    /// Manages file library discovery for MIDI songs and SoundFonts (.sf2).
    /// Scans Songs/ and SoundFonts/ folders and provides in-game file selection.
    /// </summary>
    public class FileLibraryManager
    {
        public string BaseDirectory { get; }
        public string SongsDirectory { get; }
        public string SoundFontsDirectory { get; }

        public List<string> DiscoveredMidiFiles { get; private set; } = new();
        public List<string> DiscoveredSoundFontFiles { get; private set; } = new();

        public int SelectedMidiIndex { get; set; } = 0;
        public int SelectedSoundFontIndex { get; set; } = 0;

        public FileLibraryManager()
        {
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            SongsDirectory = Path.Combine(BaseDirectory, "Songs");
            SoundFontsDirectory = Path.Combine(BaseDirectory, "SoundFonts");

            EnsureDirectoriesExist();
            RefreshLibrary();
        }

        public void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(SongsDirectory))
                {
                    Directory.CreateDirectory(SongsDirectory);
                    string readme = Path.Combine(SongsDirectory, "README.txt");
                    if (!File.Exists(readme))
                    {
                        File.WriteAllText(readme, "Place your .mid or .midi files in this folder to load them in AxeWii.\nPress [O] in-game to import from anywhere on your drive.");
                    }
                }

                if (!Directory.Exists(SoundFontsDirectory))
                {
                    Directory.CreateDirectory(SoundFontsDirectory);
                    string readme = Path.Combine(SoundFontsDirectory, "README.txt");
                    if (!File.Exists(readme))
                    {
                        File.WriteAllText(readme, "Place your .sf2 SoundFont files in this folder to use custom soundbanks.\nPress [S] or [O] in-game to import from anywhere on your drive.");
                    }
                }
            }
            catch { }
        }

        public void RefreshLibrary()
        {
            EnsureDirectoriesExist();

            // 1. Discover MIDI files
            var midiList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(SongsDirectory))
            {
                foreach (var f in Directory.GetFiles(SongsDirectory, "*.mid", SearchOption.AllDirectories)) midiList.Add(f);
                foreach (var f in Directory.GetFiles(SongsDirectory, "*.midi", SearchOption.AllDirectories)) midiList.Add(f);
            }

            foreach (var f in Directory.GetFiles(BaseDirectory, "*.mid", SearchOption.TopDirectoryOnly)) midiList.Add(f);
            foreach (var f in Directory.GetFiles(BaseDirectory, "*.midi", SearchOption.TopDirectoryOnly)) midiList.Add(f);

            DiscoveredMidiFiles = midiList.OrderBy(f => Path.GetFileName(f)).ToList();

            // 2. Discover SoundFont files
            var sfList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(SoundFontsDirectory))
            {
                foreach (var f in Directory.GetFiles(SoundFontsDirectory, "*.sf2", SearchOption.AllDirectories)) sfList.Add(f);
            }

            foreach (var f in Directory.GetFiles(BaseDirectory, "*.sf2", SearchOption.TopDirectoryOnly)) sfList.Add(f);

            DiscoveredSoundFontFiles = sfList.OrderBy(f => Path.GetFileName(f)).ToList();
        }

        public string? GetCurrentMidiFile()
        {
            if (DiscoveredMidiFiles.Count == 0) return null;
            SelectedMidiIndex = Math.Clamp(SelectedMidiIndex, 0, DiscoveredMidiFiles.Count - 1);
            return DiscoveredMidiFiles[SelectedMidiIndex];
        }

        public string? GetCurrentSoundFontFile()
        {
            if (DiscoveredSoundFontFiles.Count == 0) return null;
            SelectedSoundFontIndex = Math.Clamp(SelectedSoundFontIndex, 0, DiscoveredSoundFontFiles.Count - 1);
            return DiscoveredSoundFontFiles[SelectedSoundFontIndex];
        }
    }
}
