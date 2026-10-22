using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace AxeWii.Hardware
{
    public struct GuitarFrets
    {
        public bool Green;
        public bool Red;
        public bool Yellow;
        public bool Blue;
        public bool Orange;
        public bool Open;

        public bool AnyHeld => Green || Red || Yellow || Blue || Orange;

        public string GetChordKey()
        {
            if (!AnyHeld) return "open";
            string k = "";
            if (Green) k += "G";
            if (Red) k += "R";
            if (Yellow) k += "Y";
            if (Blue) k += "B";
            if (Orange) k += "O";
            return k;
        }
    }

    public class GuitarState
    {
        public GuitarFrets Frets;
        public bool StrumUp;
        public bool StrumDown;
        public float Whammy; // 0.0 to 1.0
        public float JoystickX; // -1.0 to 1.0
        public float JoystickY; // -1.0 to 1.0
        public bool ButtonMinus; // Tare / Recalibrate
        public bool ButtonPlus;  // Reset Tunings
        public bool Connected;
        public string DeviceName = "Disconnected";

        // Accelerometer in G-force units
        public float AccelX;
        public float AccelY;
        public float AccelZ;
    }

    public enum WebHidPreset
    {
        UserCustom,      // Default per WiiGuitarTester (user model)
        StandardLesPaul  // Standard WiiBrew
    }

    /// <summary>
    /// Direct Bluetooth HID Manager for Nintendo Wii Remote with Guitar Hero 3 Extension.
    /// Based on https://khalifeserge.github.io/WiiGuitarTester/
    /// No USB dongles or middleman software required.
    /// </summary>
    public class WiiGuitarHidManager : IDisposable
    {
        public GuitarState State { get; private set; } = new GuitarState();
        public WebHidPreset Preset { get; set; } = WebHidPreset.UserCustom;
        public bool ActiveLow { get; set; } = true;

        public event Action<GuitarState>? OnStateChanged;
        public event Action<string>? OnStrumHit; // "UP" or "DOWN"
        public event Action? OnTareMotion;      // Minus button
        public event Action? OnResetTunings;    // Plus button

        private HidDevice? _device;
        private HidStream? _stream;
        private CancellationTokenSource? _cts;
        private Thread? _readThread;
        private Timer? _keepAliveTimer;

        private bool _lastStrumUp;
        private bool _lastStrumDown;
        private bool _lastMinus;
        private bool _lastPlus;

        // Nintendo Wiimote Vendor ID & Product IDs
        private const int VendorIdNintendo = 0x057e;
        private const int ProductIdWiimote = 0x0306;
        private const int ProductIdWiimotePlus = 0x0330;

        public bool Connect()
        {
            try
            {
                foreach (var dev in DeviceList.Local.GetHidDevices())
                {
                    if (dev.VendorID == VendorIdNintendo && 
                       (dev.ProductID == ProductIdWiimote || dev.ProductID == ProductIdWiimotePlus || dev.ProductID == 0))
                    {
                        if (dev.TryOpen(out _stream))
                        {
                            _device = dev;
                            State.Connected = true;
                            State.DeviceName = dev.GetProductName() ?? "Nintendo Wiimote (Direct Bluetooth)";

                            // Initialize Wiimote + GH3 Extension
                            InitializeWiimoteHandshake();

                            _cts = new CancellationTokenSource();
                            _readThread = new Thread(ReadLoop) { IsBackground = true };
                            _readThread.Start();

                            // 4-second keep-alive loop
                            _keepAliveTimer = new Timer(SendKeepAlive, null, 4000, 4000);

                            OnStateChanged?.Invoke(State);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AxeWii] Connect error: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Handshake matching WiiGuitarTester:
        /// 1. Set LED 1 (Report 0x11, [0x10])
        /// 2. Data Mode 0x35 (Report 0x12, [0x04, 0x35])
        /// 3. Unencrypt extension registers (Report 0x16, 21-byte packets)
        /// </summary>
        public void InitializeWiimoteHandshake()
        {
            if (_stream == null) return;
            try
            {
                // 1. Set LED 1
                _stream.Write(new byte[] { 0x11, 0x10 });

                // 2. Continuous Reporting Mode 0x35 (Buttons + Accelerometer + 16 Extension Bytes)
                _stream.Write(new byte[] { 0x12, 0x04, 0x35 });
                Thread.Sleep(50);

                // 3. Unencrypt Extension: Write 0x55 to 0x04a400f0 (21 bytes)
                byte[] p1 = new byte[22];
                p1[0] = 0x16; // Report ID
                p1[1] = 0x04; p1[2] = 0xa4; p1[3] = 0x00; p1[4] = 0xf0; p1[5] = 0x01; p1[6] = 0x55;
                _stream.Write(p1);
                Thread.Sleep(50);

                // Write 0x00 to 0x04a400fb (21 bytes)
                byte[] p2 = new byte[22];
                p2[0] = 0x16;
                p2[1] = 0x04; p2[2] = 0xa4; p2[3] = 0x00; p2[4] = 0xfb; p2[5] = 0x01; p2[6] = 0x00;
                _stream.Write(p2);
                Thread.Sleep(50);

                // Legacy fallback: Write 0x00 to 0x04a40040 (21 bytes)
                byte[] p3 = new byte[22];
                p3[0] = 0x16;
                p3[1] = 0x04; p3[2] = 0xa4; p3[3] = 0x00; p3[4] = 0x40; p3[5] = 0x01; p3[6] = 0x00;
                _stream.Write(p3);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AxeWii] Handshake notice: {ex.Message}");
            }
        }

        private void SendKeepAlive(object? state)
        {
            if (_stream != null && State.Connected)
            {
                try
                {
                    _stream.Write(new byte[] { 0x12, 0x00, 0x35 });
                }
                catch { }
            }
        }

        public void TestRumble()
        {
            if (_stream == null) return;
            try
            {
                _stream.Write(new byte[] { 0x11, 0x11 });
                Task.Delay(250).ContinueWith(_ =>
                {
                    try { _stream?.Write(new byte[] { 0x11, 0x10 }); } catch { }
                });
            }
            catch { }
        }

        private void ReadLoop()
        {
            byte[] buffer = new byte[64];
            while (_cts != null && !_cts.IsCancellationRequested && _stream != null)
            {
                try
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ParseInputReport(buffer, bytesRead);
                    }
                }
                catch (IOException)
                {
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }

            State.Connected = false;
            State.DeviceName = "Disconnected";
            OnStateChanged?.Invoke(State);
        }

        private void ParseInputReport(byte[] data, int length)
        {
            if (length < 4) return;
            byte reportId = data[0];

            // In reports 0x35, 0x34, 0x32
            if (reportId == 0x35 || reportId == 0x34 || reportId == 0x32 || reportId == 0x00)
            {
                int payloadOffset = reportId == 0x00 ? 0 : 1;
                byte b0 = data[payloadOffset + 0];
                byte b1 = data[payloadOffset + 1];

                bool corePlus = (b0 & 0x10) != 0;
                bool coreMinus = (b1 & 0x10) != 0;

                // Accelerometer (Bytes 2, 3, 4 in Report 0x35)
                if (length >= payloadOffset + 5)
                {
                    State.AccelX = (data[payloadOffset + 2] - 128) / 26.0f;
                    State.AccelY = (data[payloadOffset + 3] - 128) / 26.0f;
                    State.AccelZ = (data[payloadOffset + 4] - 128) / 26.0f;
                }

                // Extension Bytes
                int extOffset = (reportId == 0x35 || length >= 12) ? payloadOffset + 5 : payloadOffset + 2;
                if (length >= extOffset + 6)
                {
                    byte[] ext = new byte[6];
                    Array.Copy(data, extOffset, ext, 0, 6);
                    ParseExtensionData(ext, corePlus, coreMinus);
                }
            }
        }

        private void ParseExtensionData(byte[] ext, bool corePlus, bool coreMinus)
        {
            bool IsPressed(byte val, byte mask)
            {
                bool match = (val & mask) != 0;
                return ActiveLow ? !match : match;
            }

            bool g, r, y, b, o, strumUp, strumDown;

            if (Preset == WebHidPreset.UserCustom)
            {
                // User model mapping from WiiGuitarTester:
                // Green:  Ext Byte 5, Bit 4 (0x10)
                // Red:    Ext Byte 5, Bit 6 (0x40)
                // Yellow: Ext Byte 5, Bit 3 (0x08)
                // Blue:   Ext Byte 5, Bit 5 (0x20)
                // Orange: Ext Byte 5, Bit 7 (0x80)
                g = IsPressed(ext[5], 0x10);
                r = IsPressed(ext[5], 0x40);
                y = IsPressed(ext[5], 0x08);
                b = IsPressed(ext[5], 0x20);
                o = IsPressed(ext[5], 0x80);

                // Strum Up: Ext Byte 5, Bit 0 (0x01)
                // Strum Down: Ext Byte 4, Bit 6 (0x40) or Ext Byte 5, Bit 1 (0x02)
                strumUp = IsPressed(ext[5], 0x01);
                strumDown = IsPressed(ext[4], 0x40) || IsPressed(ext[5], 0x02);
            }
            else
            {
                // Standard Les Paul / WiiBrew:
                g = IsPressed(ext[4], 0x40);
                r = IsPressed(ext[4], 0x10);
                y = IsPressed(ext[4], 0x02);
                b = IsPressed(ext[4], 0x04);
                o = IsPressed(ext[4], 0x08);
                strumUp = IsPressed(ext[4], 0x80);
                strumDown = IsPressed(ext[4], 0x20);
            }

            bool anyFret = g || r || y || b || o;
            bool open = (strumUp || strumDown) && !anyFret;

            // Minus: Ext Byte 4 Bit 4 (0x10), Plus: Ext Byte 4 Bit 2 (0x04)
            bool minus = IsPressed(ext[4], 0x10) || coreMinus;
            bool plus = IsPressed(ext[4], 0x04) || corePlus;

            // Whammy: Ext Byte 3 & 0x1F (0..31)
            int whammyRaw = ext[3] & 0x1F;
            float whammy = Math.Clamp(whammyRaw / 31.0f, 0.0f, 1.0f);

            // Joystick: Ext Byte 0 & 1 (0..63, center ~32)
            int rawSX = ext[0] & 0x3F;
            int rawSY = ext[1] & 0x3F;
            float joyX = Math.Clamp((rawSX - 32) / 26.0f, -1.0f, 1.0f);
            float joyY = Math.Clamp((rawSY - 32) / 26.0f, -1.0f, 1.0f);

            State.Frets = new GuitarFrets
            {
                Green = g,
                Red = r,
                Yellow = y,
                Blue = b,
                Orange = o,
                Open = open
            };
            State.StrumUp = strumUp;
            State.StrumDown = strumDown;
            State.Whammy = whammy;
            State.JoystickX = Math.Abs(joyX) > 0.15f ? joyX : 0f;
            State.JoystickY = Math.Abs(joyY) > 0.15f ? joyY : 0f;
            State.ButtonMinus = minus;
            State.ButtonPlus = plus;

            // Trigger events
            if (strumUp && !_lastStrumUp) OnStrumHit?.Invoke("UP");
            if (strumDown && !_lastStrumDown) OnStrumHit?.Invoke("DOWN");
            if (minus && !_lastMinus) OnTareMotion?.Invoke();
            if (plus && !_lastPlus) OnResetTunings?.Invoke();

            _lastStrumUp = strumUp;
            _lastStrumDown = strumDown;
            _lastMinus = minus;
            _lastPlus = plus;

            OnStateChanged?.Invoke(State);
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            _keepAliveTimer?.Dispose();
            _keepAliveTimer = null;
            _stream?.Close();
            _stream = null;
            _device = null;
            State.Connected = false;
            OnStateChanged?.Invoke(State);
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
        }
    }
}
