using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace AxeWii.Core
{
    /// <summary>
    /// Native OS File Dialog for importing .mid and .sf2 files on desktop.
    /// Operates on a dedicated STA thread without requiring WinForms or WPF dependencies.
    /// </summary>
    public static class NativeFileDialog
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string? filter = null;
            public string? customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public string? file = null;
            public int maxFile = 0;
            public string? fileTitle = null;
            public int maxFileTitle = 0;
            public string? initialDir = null;
            public string? title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string? defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string? templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        public static string? OpenFile(string title, string filter, string defaultExt)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string? chosenFile = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        var ofn = new OpenFileName();
                        ofn.structSize = Marshal.SizeOf(ofn);
                        // Windows filter format: Description\0Pattern\0Description\0Pattern\0\0
                        ofn.filter = filter.Replace('|', '\0') + "\0\0";
                        ofn.file = new string(new char[2048]);
                        ofn.maxFile = ofn.file.Length;
                        ofn.title = title;
                        ofn.defExt = defaultExt;
                        ofn.initialDir = AppDomain.CurrentDomain.BaseDirectory;
                        // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
                        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

                        if (GetOpenFileName(ofn))
                        {
                            chosenFile = ofn.file?.TrimEnd('\0');
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AxeWii] File dialog note: {ex.Message}");
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(120000); // 2 minute timeout for user choice
                return string.IsNullOrWhiteSpace(chosenFile) ? null : chosenFile;
            }
            return null;
        }

        public static string? OpenMidiDialog()
        {
            return OpenFile(
                "Import MIDI Song File (*.mid;*.midi)",
                "MIDI Files (*.mid;*.midi)|*.mid;*.midi|All Files (*.*)|*.*",
                "mid"
            );
        }

        public static string? OpenSoundFontDialog()
        {
            return OpenFile(
                "Import SoundFont 2 Bank (*.sf2)",
                "SoundFont 2 Files (*.sf2)|*.sf2|All Files (*.*)|*.*",
                "sf2"
            );
        }

        public static string? OpenAudioOrMidiDialog()
        {
            return OpenFile(
                "Import MIDI Song or SoundFont 2 File",
                "Supported Music Files (*.mid;*.midi;*.sf2)|*.mid;*.midi;*.sf2|MIDI Files (*.mid;*.midi)|*.mid;*.midi|SoundFont 2 (*.sf2)|*.sf2|All Files (*.*)|*.*",
                "mid"
            );
        }
    }
}
