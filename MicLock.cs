using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("MicLock")]
[assembly: AssemblyDescription("Recording device level locker")]
[assembly: AssemblyProduct("MicLock")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace MicLockApp
{
    internal static class Col
    {
        public static readonly Color Bg = Color.FromArgb(18, 18, 18);
        public static readonly Color Surface = Color.FromArgb(28, 28, 28);
        public static readonly Color Elevated = Color.FromArgb(36, 36, 36);
        public static readonly Color Fg = Color.FromArgb(236, 236, 236);
        public static readonly Color Muted = Color.FromArgb(141, 141, 147);
        public static readonly Color Faint = Color.FromArgb(90, 90, 96);
        public static readonly Color Accent = Color.FromArgb(45, 212, 191);
        public static readonly Color Border = Color.FromArgb(46, 46, 50);
        public static readonly Color Ready = Color.FromArgb(74, 222, 128);
        public static readonly Color Track = Color.FromArgb(42, 42, 46);
        public static readonly Color Input = Color.FromArgb(22, 22, 24);
        public static readonly Color Disabled = Color.FromArgb(248, 113, 113);
    }

    internal static class Program
    {
        internal const string MutexName = "Local\\MicLock.SingleInstance";
        internal static Guid EventCtx = new Guid("B7E6C9A1-4F2D-4A11-9C3E-7A1D2B8E4F01");
        internal static uint WM_SHOWME;
        static Mutex _mutex;

        [STAThread]
        static void Main()
        {
            try { Native.SetProcessDPIAware(); }
            catch { }

            bool startTray = false;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a == null) continue;
                if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "-tray", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/minimized", StringComparison.OrdinalIgnoreCase))
                    startTray = true;
            }

            try
            {
                WM_SHOWME = Native.RegisterWindowMessage("MicLock.WM_SHOWME");
                bool created;
                _mutex = new Mutex(true, MutexName, out created);
                if (!created)
                {
                    IntPtr ping = Native.FindWindow(null, PingWindow.Caption);
                    if (ping != IntPtr.Zero)
                        Native.PostMessage(ping, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    Native.PostMessage((IntPtr)0xFFFF, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    if (ex != null) Log(ex);
                };

                Application.Run(new AppContext(startTray));
            }
            catch (Exception ex)
            {
                Log(ex);
                try
                {
                    MessageBox.Show(ex.ToString(), "MicLock failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
            finally
            {
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); }
                    catch { }
                    try { _mutex.Close(); }
                    catch { }
                    _mutex = null;
                }
            }
        }

        internal static void Log(Exception ex)
        {
            AppendLog(ex.ToString());
        }

        internal static void LogMsg(string msg)
        {
            AppendLog(msg);
        }

        static void AppendLog(string line)
        {
            try
            {
                string dir = Settings.Dir();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "error.log");
                List<string> lines = new List<string>();
                if (File.Exists(path))
                {
                    string[] old = File.ReadAllLines(path);
                    for (int i = 0; i < old.Length; i++) lines.Add(old[i]);
                }
                lines.Add(DateTime.Now.ToString("u") + " " + line);
                int extra = lines.Count - 100;
                if (extra > 0) lines.RemoveRange(0, extra);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch { }
        }
    }

    internal sealed class AppContext : ApplicationContext
    {
        internal static MainForm Window;
        PingWindow _ping;

        public AppContext(bool startTray)
        {
            Window = new MainForm();
            _ping = new PingWindow();
            if (startTray) Window.GoToTray();
            else Window.ShowWindow();
        }
    }

    internal sealed class PingWindow : NativeWindow
    {
        public const string Caption = "MicLock.WM_SHOWME.Listener";

        public PingWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = Caption;
            cp.Style = unchecked((int)0x80000000); // WS_POPUP
            cp.ExStyle = 0x80; // WS_EX_TOOLWINDOW — no taskbar
            cp.X = -32000;
            cp.Y = -32000;
            cp.Width = 0;
            cp.Height = 0;
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.WM_SHOWME)
            {
                MainForm f = AppContext.Window;
                if (f != null)
                {
                    try
                    {
                        if (f.IsHandleCreated && f.InvokeRequired)
                            f.BeginInvoke(new MethodInvoker(f.ShowWindow));
                        else
                            f.ShowWindow();
                    }
                    catch { }
                }
                return;
            }
            base.WndProc(ref m);
        }
    }

    internal static class Native
    {
        public const int CLSCTX_ALL = 23;
        public const int CLSCTX_INPROC = 1;
        public const int STGM_READ = 0;
        public const int eCapture = 1;
        public const int DEVICE_STATE_ACTIVE = 0x1;
        public const int DEVICE_STATE_DISABLED = 0x2;
        public const int DEVICE_STATE_NOTPRESENT = 0x4;
        public const int DEVICE_STATE_UNPLUGGED = 0x8;
        public const int DEVICE_STATEMASK_ALL = 0xF;
        public const int DEVICE_STATEMASK_RECORDING = 0x1 | 0x2 | 0x8;
        public const int SB_VERT = 1;
        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_NOMOVE = 0x0002;
        public const int SWP_NOZORDER = 0x0004;
        public const int SWP_FRAMECHANGED = 0x0020;
        public const int AUDCLNT_SHAREMODE_SHARED = 0;
        public const int AUDCLNT_STREAMFLAGS_NOPERSIST = 0x00020000;
        public const int AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = unchecked((int)0x80000000);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern int CoCreateInstance(ref Guid clsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid iid, out IntPtr ppv);

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PropVariant pvar);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        public static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public const int SB_HORZ = 0;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void DarkScroll(IntPtr hwnd)
        {
            try { SetWindowTheme(hwnd, "DarkMode_Explorer", null); }
            catch { }
            try { ShowScrollBar(hwnd, SB_HORZ, false); }
            catch { }
        }

        public static void DarkTitle(IntPtr hwnd)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(hwnd, 20, ref on, 4);
                DwmSetWindowAttribute(hwnd, 19, ref on, 4);
            }
            catch { }
        }

        public static void Release(object o)
        {
            if (o == null) return;
            try { Marshal.ReleaseComObject(o); }
            catch { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
        public static PropertyKey FriendlyName = new PropertyKey
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = 14
        };
        public static PropertyKey DeviceDesc = new PropertyKey
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = 2
        };
        public static PropertyKey InterfaceName = new PropertyKey
        {
            fmtid = new Guid("026E516E-B814-414B-83CD-856D6FEF4822"),
            pid = 2
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public short vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioVolumeNotificationData
    {
        public Guid guidEventContext;
        public int bMuted;
        public float fMasterVolume;
        public uint nChannels;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorCom { }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class CPolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, [MarshalAs(UnmanagedType.Interface)] out IMMDeviceCollection ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint pcDevices);
        [PreserveSig] int Item(uint nDevice, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppProperties);
        [PreserveSig] int GetId(out IntPtr ppstrId);
        [PreserveSig] int GetState(out uint pdwState);
    }

    [ComImport, Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMEndpoint
    {
        [PreserveSig] int GetDataFlow(out int pDataFlow);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, uint dwNewState);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PropertyKey key);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int GetChannelCount(out int pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr pNotify);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float pfPeak);
        [PreserveSig] int GetMeteringChannelCount(out uint pnChannelCount);
        [PreserveSig] int GetChannelsPeakValues(uint u32ChannelCount, [Out] float[] afPeakValues);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
        [PreserveSig] int GetStreamLatency(out long phnsLatency);
        [PreserveSig] int GetCurrentPadding(out int pNumPaddingFrames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
        [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out int pdwFlags, out long pu64DevicePosition, out long pu64QPCPosition);
        [PreserveSig] int ReleaseBuffer(uint numFramesRead);
        [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceTopology
    {
        [PreserveSig] int GetConnectorCount(out uint pCount);
        [PreserveSig] int GetConnector(uint nIndex, out IConnector ppConnector);
        [PreserveSig] int GetSubunitCount(out uint pCount);
        [PreserveSig] int GetSubunit(uint nIndex, out IPart ppPart);
        [PreserveSig] int GetPartById(uint nId, out IPart ppPart);
        [PreserveSig] int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrDeviceId);
        [PreserveSig] int GetSignalPath(IPart pIPartFrom, IPart pIPartTo, bool bRejectMixedPaths, out IPartsList ppParts);
    }

    [ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IConnector
    {
        [PreserveSig] int GetType(out int pType);
        [PreserveSig] int GetDataFlow(out int pFlow);
        [PreserveSig] int ConnectTo(IConnector pConnectTo);
        [PreserveSig] int Disconnect();
        [PreserveSig] int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool pbConnected);
        [PreserveSig] int GetConnectedTo(out IConnector ppConTo);
        [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrConnectorId);
        [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrDeviceId);
    }

    [ComImport, Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPart
    {
        [PreserveSig] int GetName([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrName);
        [PreserveSig] int GetLocalId(out uint pnId);
        [PreserveSig] int GetGlobalId([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrGlobalId);
        [PreserveSig] int GetPartType(out int pPartType);
        [PreserveSig] int GetSubType(out Guid pSubType);
        [PreserveSig] int GetControlInterfaceCount(out uint pCount);
        [PreserveSig] int GetControlInterface(uint nIndex, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterfaceDesc);
        [PreserveSig] int EnumPartsIncoming(out IPartsList ppParts);
        [PreserveSig] int EnumPartsOutgoing(out IPartsList ppParts);
        [PreserveSig] int GetTopologyObject([MarshalAs(UnmanagedType.IUnknown)] out object ppTopology);
        [PreserveSig] int Activate(int dwClsContext, ref Guid refiid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
        [PreserveSig] int RegisterControlChangeCallback(ref Guid riid, IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeCallback(IntPtr pNotify);
    }

    [ComImport, Guid("6DAA848C-5EB0-45CC-AEA5-998A2CDA1FFB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPartsList
    {
        [PreserveSig] int GetCount(out uint pCount);
        [PreserveSig] int GetPart(uint nIndex, out IPart ppPart);
    }

    [ComImport, Guid("C2F8E001-F205-4C24-B56A-5F3AE4B510B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPerChannelDbLevel
    {
        [PreserveSig] int GetChannelCount(out uint pcChannels);
        [PreserveSig] int GetLevelRange(uint nChannel, out float pflMinLevelDB, out float pflMaxLevelDB, out float pflStepping);
        [PreserveSig] int GetLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int SetLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetLevelUniform(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetLevelAllChannels([MarshalAs(UnmanagedType.LPArray)] float[] aLevelsDB, uint cChannels, ref Guid pguidEventContext);
    }

    [ComImport, Guid("7FB7B48F-531D-44A2-BCB3-5AD5A134B3DC"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioVolumeLevel
    {
        [PreserveSig] int GetChannelCount(out uint pcChannels);
        [PreserveSig] int GetLevelRange(uint nChannel, out float pflMinLevelDB, out float pflMaxLevelDB, out float pflStepping);
        [PreserveSig] int GetLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int SetLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetLevelUniform(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetLevelAllChannels([MarshalAs(UnmanagedType.LPArray)] float[] aLevelsDB, uint cChannels, ref Guid pguidEventContext);
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        void U1(); void U2(); void U3(); void U4(); void U5(); void U6(); void U7(); void U8();
        void U9(); void U10(); void U11();
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int isVisible);
    }

    [ComImport, Guid("CA286FC3-91FD-42C3-8E9B-CAAFA66242E3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig10
    {
        void U1(); void U2(); void U3(); void U4(); void U5(); void U6(); void U7(); void U8();
        void U9(); void U10(); void U11();
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int isVisible);
    }

    [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfigVista
    {
        void U1(); void U2(); void U3(); void U4(); void U5(); void U6(); void U7(); void U8();
        void U9(); void U10(); void U11();
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int isVisible);
    }

    internal sealed class DevState
    {
        public string Id;
        public string Name;
        public uint WinState;
        public bool Lock;
        public bool HasVolTarget;
        public bool HasMuteTarget;
        public bool HasBoostTarget;
        public float VolTarget;
        public bool MuteTarget;
        public float BoostTarget;
        public bool HasBoost;
        public float BoostMin;
        public float BoostMax;
        public float BoostStep;
        public IMMDevice Device;
        public IAudioEndpointVolume Volume;
        public IAudioMeterInformation Meter;
        public IPerChannelDbLevel Boost;
        public IAudioClient Client;
        public IAudioCaptureClient Capture;
        public VolWatch Watch;
        public float LastPeak;
        public float PeakHold;
        public bool BoostTried;
        public DateTime LastBalloon = DateTime.MinValue;
        public bool Enabled
        {
            get { return (WinState & Native.DEVICE_STATE_DISABLED) == 0; }
        }
        public bool Active
        {
            get { return (WinState & Native.DEVICE_STATE_ACTIVE) != 0; }
        }
    }

    internal sealed class VolWatch : IAudioEndpointVolumeCallback
    {
        public string Id;
        public Engine Eng;
        public int OnNotify(IntPtr pNotify)
        {
            try
            {
                AudioVolumeNotificationData data = (AudioVolumeNotificationData)Marshal.PtrToStructure(pNotify, typeof(AudioVolumeNotificationData));
                Eng.OnVolumeNotify(Id, data);
            }
            catch { }
            return 0;
        }
    }

    internal sealed class NotifyClient : IMMNotificationClient
    {
        public Engine Eng;
        public int OnDeviceStateChanged(string id, uint state) { Eng.RequestRefresh(); return 0; }
        public int OnDeviceAdded(string id) { Eng.RequestRefresh(); return 0; }
        public int OnDeviceRemoved(string id) { Eng.RequestRefresh(); return 0; }
        public int OnDefaultDeviceChanged(int flow, int role, string id) { return 0; }
        public int OnPropertyValueChanged(string id, PropertyKey key) { Eng.RequestRefresh(); return 0; }
    }

    internal sealed class Engine
    {
        public event Action DevicesChanged;
        public event Action<string> Restored;
        readonly object _gate = new object();
        readonly Dictionary<string, DevState> _map = new Dictionary<string, DevState>(StringComparer.OrdinalIgnoreCase);
        IMMDeviceEnumerator _enum;
        NotifyClient _notify;
        SynchronizationContext _ui;
        volatile bool _refresh;
        bool _meters;
        bool _disposed;
        bool _notifyReg;
        public string LastError;

        static readonly Guid CLSID_MM = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        static readonly Guid IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");
        static readonly Guid IID_Enum = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
        static readonly Guid IID_EnumOld = new Guid("A9563331-627B-11D1-AF52-00AA00A74B8F");
        static readonly Guid IID_Vol = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static readonly Guid IID_Meter = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        static readonly Guid IID_Client = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        static readonly Guid IID_Capture = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
        static readonly Guid IID_Topo = new Guid("2A07407E-6497-4A18-9787-32F79BD0D98F");
        static readonly Guid IID_Boost = new Guid("7FB7B48F-531D-44A2-BCB3-5AD5A134B3DC");
        static readonly Guid IID_PerCh = new Guid("C2F8E001-F205-4C24-B56A-5F3AE4B510B5");
        static readonly Guid IID_Part = new Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9");

        public Engine()
        {
            LastError = null;
            _notify = new NotifyClient();
            _notify.Eng = this;
            _enum = CreateEnumerator();
        }

        IMMDeviceEnumerator CreateEnumerator()
        {
            try
            {
                IMMDeviceEnumerator e = new MMDeviceEnumeratorCom() as IMMDeviceEnumerator;
                if (e != null) return e;
            }
            catch (Exception ex) { Program.Log(ex); }

            try
            {
                Type t = Type.GetTypeFromCLSID(CLSID_MM, false);
                if (t != null)
                {
                    object o = Activator.CreateInstance(t);
                    IMMDeviceEnumerator e = o as IMMDeviceEnumerator;
                    if (e != null) return e;
                    Native.Release(o);
                }
            }
            catch (Exception ex) { Program.Log(ex); }

            try
            {
                Guid clsid = CLSID_MM;
                Guid iidUnk = IID_IUnknown;
                IntPtr pUnk;
                int hr = Native.CoCreateInstance(ref clsid, IntPtr.Zero, 23, ref iidUnk, out pUnk);
                Program.LogMsg("CoCreate IUnknown hr=0x" + hr.ToString("X8"));
                if (hr == 0 && pUnk != IntPtr.Zero)
                {
                    try
                    {
                        Guid g = IID_Enum;
                        IntPtr pEnum;
                        int q = Marshal.QueryInterface(pUnk, ref g, out pEnum);
                        Program.LogMsg("QI A95664D2 hr=0x" + q.ToString("X8"));
                        if (q != 0)
                        {
                            g = IID_EnumOld;
                            q = Marshal.QueryInterface(pUnk, ref g, out pEnum);
                            Program.LogMsg("QI A9563331 hr=0x" + q.ToString("X8"));
                        }
                        if (q == 0 && pEnum != IntPtr.Zero)
                        {
                            object o = Marshal.GetTypedObjectForIUnknown(pEnum, typeof(IMMDeviceEnumerator));
                            Marshal.Release(pEnum);
                            IMMDeviceEnumerator e = o as IMMDeviceEnumerator;
                            if (e != null) return e;
                        }
                    }
                    finally { Marshal.Release(pUnk); }
                }
                LastError = "CoCreateInstance hr=0x" + hr.ToString("X8");
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                LastError = ex.Message;
            }

            if (string.IsNullOrEmpty(LastError)) LastError = "Could not create IMMDeviceEnumerator";
            Program.LogMsg("enumerator failed: " + LastError);
            return null;
        }

        public void AttachUi()
        {
            if (SynchronizationContext.Current != null)
                _ui = SynchronizationContext.Current;
            if (_notifyReg) return;
            try
            {
                if (_enum != null && _notify != null)
                {
                    _enum.RegisterEndpointNotificationCallback(_notify);
                    _notifyReg = true;
                }
            }
            catch (Exception ex)
            {
                Program.Log(ex);
            }
        }

        public void RequestRefresh()
        {
            _refresh = true;
        }

        public bool ConsumeRefresh()
        {
            if (!_refresh) return false;
            _refresh = false;
            return true;
        }

        public void SetMetersActive(bool on)
        {
            lock (_gate)
            {
                _meters = on;
                if (!on)
                {
                    foreach (KeyValuePair<string, DevState> kv in _map)
                        StopMeter(kv.Value);
                }
            }
        }

        public List<DevState> List(bool all, bool refresh)
        {
            if (refresh) Rebuild();
            List<DevState> list = new List<DevState>();
            lock (_gate)
            {
                foreach (KeyValuePair<string, DevState> kv in _map)
                {
                    DevState d = kv.Value;
                    if (!all && !d.Active) continue;
                    if ((d.WinState & Native.DEVICE_STATE_NOTPRESENT) != 0) continue;
                    list.Add(d);
                }
            }
            list.Sort(delegate(DevState a, DevState b)
            {
                int c = StatusRank(a).CompareTo(StatusRank(b));
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        static int StatusRank(DevState d)
        {
            if ((d.WinState & Native.DEVICE_STATE_UNPLUGGED) != 0) return 2;
            if ((d.WinState & Native.DEVICE_STATE_DISABLED) != 0) return 1;
            return 0;
        }

        public int TotalCount()
        {
            lock (_gate) { return _map.Count; }
        }

        void Rebuild()
        {
            if (_enum == null)
            {
                if (string.IsNullOrEmpty(LastError)) LastError = "No audio enumerator";
                return;
            }
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            IMMDeviceCollection col = null;
            try
            {
                int hr = _enum.EnumAudioEndpoints(Native.eCapture, Native.DEVICE_STATEMASK_RECORDING, out col);
                if (hr != 0 || col == null)
                {
                    string m = "EnumAudioEndpoints hr=0x" + hr.ToString("X8");
                    if (LastError != m) Program.LogMsg(m);
                    LastError = m;
                    return;
                }
                uint n = 0;
                hr = col.GetCount(out n);
                if (hr != 0)
                {
                    LastError = "GetCount hr=0x" + hr.ToString("X8");
                    Program.LogMsg(LastError);
                    return;
                }
                if (n > 256) n = 256;
                if (n == 0) LastError = "Windows returned 0 capture endpoints";
                else LastError = null;
                for (uint i = 0; i < n; i++)
                {
                    IMMDevice dev = null;
                    try
                    {
                        if (col.Item(i, out dev) != 0 || dev == null) continue;
                        IntPtr pid;
                        if (dev.GetId(out pid) != 0 || pid == IntPtr.Zero) continue;
                        string id = Marshal.PtrToStringUni(pid);
                        Marshal.FreeCoTaskMem(pid);
                        if (string.IsNullOrEmpty(id)) continue;
                        uint state = 0;
                        dev.GetState(out state);
                        if ((state & Native.DEVICE_STATE_NOTPRESENT) != 0) continue;
                        if (!IsCapture(dev)) continue;
                        string name = ReadName(dev);
                        if (string.IsNullOrEmpty(name)) continue;
                        seen[id] = true;
                        lock (_gate)
                        {
                            DevState d;
                            if (!_map.TryGetValue(id, out d))
                            {
                                d = new DevState();
                                d.Id = id;
                                Settings.Hydrate(d);
                                _map[id] = d;
                            }
                            d.Name = name;
                            d.WinState = state;
                            try { Bind(d, dev); dev = null; }
                            catch (Exception ex)
                            {
                                Program.Log(ex);
                                if (d.Device == null) d.Device = dev;
                                dev = null;
                            }
                        }
                    }
                    catch (Exception ex) { Program.Log(ex); }
                    finally { if (dev != null) Native.Release(dev); }
                }
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                LastError = ex.Message;
            }
            finally { Native.Release(col); }

            List<DevState> drop = new List<DevState>();
            lock (_gate)
            {
                foreach (KeyValuePair<string, DevState> kv in _map)
                {
                    if (!seen.ContainsKey(kv.Key)) drop.Add(kv.Value);
                }
                for (int i = 0; i < drop.Count; i++)
                {
                    Unbind(drop[i], true);
                    _map.Remove(drop[i].Id);
                }
            }
        }

        void Bind(DevState d, IMMDevice fresh)
        {
            if (d.Device != null)
            {
                Native.Release(fresh);
                RefreshLive(d);
                return;
            }
            d.Device = fresh;
            object o;
            Guid g = IID_Vol;
            if (d.Device.Activate(ref g, Native.CLSCTX_ALL, IntPtr.Zero, out o) == 0 && o != null)
            {
                d.Volume = (IAudioEndpointVolume)o;
                d.Watch = new VolWatch();
                d.Watch.Id = d.Id;
                d.Watch.Eng = this;
                try { d.Volume.RegisterControlChangeNotify(d.Watch); }
                catch { }
            }
            g = IID_Meter;
            if (d.Device.Activate(ref g, Native.CLSCTX_ALL, IntPtr.Zero, out o) == 0 && o != null)
                d.Meter = (IAudioMeterInformation)o;
            d.BoostTried = false;
            RefreshLive(d);
            if (d.Lock) EnforceOne(d, false);
        }

        public bool ScanOneBoost()
        {
            DevState target = null;
            lock (_gate)
            {
                foreach (KeyValuePair<string, DevState> kv in _map)
                {
                    if (!kv.Value.BoostTried && kv.Value.Device != null)
                    {
                        target = kv.Value;
                        break;
                    }
                }
            }
            if (target == null) return false;
            try { BindBoost(target); }
            catch (Exception ex) { Program.Log(ex); }
            target.BoostTried = true;
            return true;
        }

        void BindBoost(DevState d)
        {
            d.HasBoost = false;
            Native.Release(d.Boost);
            d.Boost = null;
            d.Boost = FindBoost(d.Device);
            if (d.Boost == null) return;
            float min, max, step;
            if (d.Boost.GetLevelRange(0, out min, out max, out step) != 0) return;
            if (max <= 0.01f) return;
            d.HasBoost = true;
            d.BoostMin = min;
            d.BoostMax = max;
            d.BoostStep = step > 0 ? step : 1f;
        }

        void Unbind(DevState d, bool releaseDevice)
        {
            StopMeter(d);
            if (d.Volume != null && d.Watch != null)
            {
                try { d.Volume.UnregisterControlChangeNotify(d.Watch); }
                catch { }
            }
            Native.Release(d.Boost); d.Boost = null;
            Native.Release(d.Meter); d.Meter = null;
            Native.Release(d.Volume); d.Volume = null;
            d.Watch = null;
            if (releaseDevice)
            {
                Native.Release(d.Device);
                d.Device = null;
                d.BoostTried = false;
                d.HasBoost = false;
            }
        }

        void StartMeter(DevState d)
        {
            if (d.Client != null) return;
            if (!d.Active) return;
            if (d.Device == null) return;
            object o;
            Guid g = IID_Client;
            if (d.Device.Activate(ref g, Native.CLSCTX_ALL, IntPtr.Zero, out o) != 0 || o == null) return;
            IAudioClient ac = (IAudioClient)o;
            IntPtr fmt = IntPtr.Zero;
            try
            {
                if (ac.GetMixFormat(out fmt) != 0 || fmt == IntPtr.Zero)
                {
                    Native.Release(ac);
                    return;
                }
                Guid sess = Guid.Empty;
                int flags = Native.AUDCLNT_STREAMFLAGS_NOPERSIST;
                int hr = ac.Initialize(Native.AUDCLNT_SHAREMODE_SHARED, flags, 1000000, 0, fmt, ref sess);
                if (hr != 0)
                {
                    Native.Release(ac);
                    g = IID_Client;
                    if (d.Device.Activate(ref g, Native.CLSCTX_ALL, IntPtr.Zero, out o) != 0 || o == null) return;
                    ac = (IAudioClient)o;
                    flags = Native.AUDCLNT_STREAMFLAGS_NOPERSIST | Native.AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM;
                    sess = Guid.Empty;
                    hr = ac.Initialize(Native.AUDCLNT_SHAREMODE_SHARED, flags, 1000000, 0, fmt, ref sess);
                }
                if (hr != 0)
                {
                    Native.Release(ac);
                    return;
                }
                g = IID_Capture;
                object cap;
                if (ac.GetService(ref g, out cap) == 0 && cap != null)
                    d.Capture = (IAudioCaptureClient)cap;
                ac.Start();
                d.Client = ac;
                ac = null;
            }
            catch
            {
                Native.Release(ac);
            }
            finally
            {
                if (fmt != IntPtr.Zero) Marshal.FreeCoTaskMem(fmt);
            }
        }

        void StopMeter(DevState d)
        {
            if (d.Client != null)
            {
                try { d.Client.Stop(); }
                catch { }
            }
            Native.Release(d.Capture); d.Capture = null;
            Native.Release(d.Client); d.Client = null;
            d.LastPeak = 0;
            d.PeakHold = 0;
        }

        public void TickMeters()
        {
            lock (_gate)
            {
                foreach (KeyValuePair<string, DevState> kv in _map)
                {
                    DevState d = kv.Value;
                    float peak = 0;
                    if (d.Meter != null)
                    {
                        try { d.Meter.GetPeakValue(out peak); }
                        catch { peak = 0; }
                    }
                    if (peak < 0) peak = 0;
                    if (peak > 1) peak = 1;
                    d.LastPeak = peak;
                    if (peak > d.PeakHold) d.PeakHold = peak;
                    else d.PeakHold = d.PeakHold * 0.92f;
                }
            }
        }

        static void Drain(DevState d)
        {
            if (d.Capture == null) return;
            try
            {
                uint pkt;
                int guard = 0;
                while (d.Capture.GetNextPacketSize(out pkt) == 0 && pkt > 0 && guard++ < 32)
                {
                    IntPtr data;
                    uint frames;
                    int flags;
                    long a, b;
                    if (d.Capture.GetBuffer(out data, out frames, out flags, out a, out b) != 0) break;
                    d.Capture.ReleaseBuffer(frames);
                }
            }
            catch { }
        }

        public void EnforceAll(bool notify)
        {
            lock (_gate)
            {
                foreach (KeyValuePair<string, DevState> kv in _map)
                    EnforceOne(kv.Value, notify);
            }
        }

        void EnforceOne(DevState d, bool notify)
        {
            if (!d.Lock) return;
            if (!d.Enabled) return;
            if (d.Volume == null) return;
            Guid ctx = Program.EventCtx;
            try
            {
                if (d.HasVolTarget)
                {
                    float v;
                    if (d.Volume.GetMasterVolumeLevelScalar(out v) == 0 && Math.Abs(v - d.VolTarget) > 0.004f)
                    {
                        d.Volume.SetMasterVolumeLevelScalar(d.VolTarget, ref ctx);
                        if (notify) FireRestored(d);
                    }
                }
                if (d.HasMuteTarget)
                {
                    bool m;
                    if (d.Volume.GetMute(out m) == 0 && m != d.MuteTarget)
                    {
                        d.Volume.SetMute(d.MuteTarget, ref ctx);
                        if (notify) FireRestored(d);
                    }
                }
            }
            catch { }
            if (d.HasBoost && d.HasBoostTarget && d.Boost != null)
            {
                try
                {
                    float b;
                    if (d.Boost.GetLevel(0, out b) == 0 && Math.Abs(b - d.BoostTarget) > 0.2f)
                    {
                        d.Boost.SetLevelUniform(d.BoostTarget, ref ctx);
                        if (notify) FireRestored(d);
                    }
                }
                catch { }
            }
        }

        void FireRestored(DevState d)
        {
            if ((DateTime.UtcNow - d.LastBalloon).TotalSeconds < 2.5) return;
            d.LastBalloon = DateTime.UtcNow;
            Action<string> h = Restored;
            if (h != null) h(d.Name);
        }

        public void OnVolumeNotify(string id, AudioVolumeNotificationData data)
        {
            if (data.guidEventContext == Program.EventCtx) return;
            if (_ui != null)
            {
                _ui.Post(delegate
                {
                    DevState d = Get(id);
                    if (d == null || !d.Lock) return;
                    bool n = Settings.Notify;
                    EnforceOne(d, n);
                }, null);
            }
        }

        public DevState Get(string id)
        {
            lock (_gate)
            {
                DevState d;
                _map.TryGetValue(id, out d);
                return d;
            }
        }

        public void RefreshLive(DevState d)
        {
            if (d.Volume != null)
            {
                try
                {
                    float v;
                    if (d.Volume.GetMasterVolumeLevelScalar(out v) == 0) { }
                    bool m;
                    d.Volume.GetMute(out m);
                }
                catch { }
            }
        }

        public bool TryGetLive(DevState d, out float vol, out bool mute, out float boost)
        {
            vol = 0;
            mute = false;
            boost = 0;
            try
            {
                if (d.Volume != null)
                {
                    d.Volume.GetMasterVolumeLevelScalar(out vol);
                    d.Volume.GetMute(out mute);
                }
                if (d.HasBoost && d.Boost != null)
                    d.Boost.GetLevel(0, out boost);
                return true;
            }
            catch { return false; }
        }

        public void UserSetVolume(DevState d, float scalar)
        {
            if (scalar < 0) scalar = 0;
            if (scalar > 1) scalar = 1;
            d.VolTarget = scalar;
            d.HasVolTarget = true;
            Guid ctx = Program.EventCtx;
            if (d.Volume != null)
            {
                try { d.Volume.SetMasterVolumeLevelScalar(scalar, ref ctx); }
                catch { }
            }
            Settings.SaveDevice(d);
        }

        public void UserSetMute(DevState d, bool mute)
        {
            d.MuteTarget = mute;
            d.HasMuteTarget = true;
            Guid ctx = Program.EventCtx;
            if (d.Volume != null)
            {
                try { d.Volume.SetMute(mute, ref ctx); }
                catch { }
            }
            Settings.SaveDevice(d);
        }

        public void UserSetBoost(DevState d, float db)
        {
            if (!d.HasBoost || d.Boost == null) return;
            if (db < d.BoostMin) db = d.BoostMin;
            if (db > d.BoostMax) db = d.BoostMax;
            if (d.BoostStep > 0.01f)
            {
                float steps = (float)Math.Round((db - d.BoostMin) / d.BoostStep);
                db = d.BoostMin + steps * d.BoostStep;
                if (db > d.BoostMax) db = d.BoostMax;
            }
            d.BoostTarget = db;
            d.HasBoostTarget = true;
            Guid ctx = Program.EventCtx;
            if (d.Boost != null)
            {
                try { d.Boost.SetLevelUniform(db, ref ctx); }
                catch { }
            }
            Settings.SaveDevice(d);
        }

        public void UserSetLock(DevState d, bool on)
        {
            d.Lock = on;
            if (on)
            {
                float v;
                bool m;
                float b;
                TryGetLive(d, out v, out m, out b);
                if (!d.HasVolTarget) { d.VolTarget = v; d.HasVolTarget = true; }
                if (!d.HasMuteTarget) { d.MuteTarget = m; d.HasMuteTarget = true; }
                if (d.HasBoost && !d.HasBoostTarget) { d.BoostTarget = b; d.HasBoostTarget = true; }
                EnforceOne(d, false);
            }
            Settings.SaveDevice(d);
        }

        public bool UserSetEnabled(DevState d, bool enabled)
        {
            try
            {
                object client = new CPolicyConfigClient();
                IPolicyConfig p = client as IPolicyConfig;
                int hr = -1;
                if (p != null) hr = p.SetEndpointVisibility(d.Id, enabled ? 1 : 0);
                if (hr != 0)
                {
                    IPolicyConfig10 p10 = client as IPolicyConfig10;
                    if (p10 != null) hr = p10.SetEndpointVisibility(d.Id, enabled ? 1 : 0);
                }
                if (hr != 0)
                {
                    IPolicyConfigVista pv = client as IPolicyConfigVista;
                    if (pv != null) hr = pv.SetEndpointVisibility(d.Id, enabled ? 1 : 0);
                }
                Native.Release(client);
                if (enabled) d.WinState = (d.WinState & ~((uint)Native.DEVICE_STATE_DISABLED)) | (uint)Native.DEVICE_STATE_ACTIVE;
                else d.WinState = (uint)Native.DEVICE_STATE_DISABLED;
                RequestRefresh();
                return hr == 0;
            }
            catch
            {
                return false;
            }
        }

        static bool IsCapture(IMMDevice dev)
        {
            try
            {
                IMMEndpoint ep = dev as IMMEndpoint;
                if (ep == null)
                {
                    IntPtr unk = Marshal.GetIUnknownForObject(dev);
                    try
                    {
                        Guid iid = new Guid("1BE09788-6894-4089-8586-9A2A6C265AC5");
                        IntPtr pp;
                        if (Marshal.QueryInterface(unk, ref iid, out pp) != 0 || pp == IntPtr.Zero) return true;
                        try
                        {
                            ep = (IMMEndpoint)Marshal.GetTypedObjectForIUnknown(pp, typeof(IMMEndpoint));
                        }
                        finally { Marshal.Release(pp); }
                    }
                    finally { Marshal.Release(unk); }
                }
                if (ep == null) return true;
                int flow;
                if (ep.GetDataFlow(out flow) != 0) return true;
                return flow == Native.eCapture;
            }
            catch { return true; }
        }

        static string ReadName(IMMDevice dev)
        {
            IPropertyStore store = null;
            try
            {
                if (dev.OpenPropertyStore(Native.STGM_READ, out store) != 0 || store == null) return null;
                string s = ReadProp(store, PropertyKey.FriendlyName);
                if (!string.IsNullOrEmpty(s)) return s;
                s = ReadProp(store, PropertyKey.DeviceDesc);
                if (!string.IsNullOrEmpty(s)) return s;
                s = ReadProp(store, PropertyKey.InterfaceName);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            catch { }
            finally { Native.Release(store); }
            return null;
        }

        static string ReadProp(IPropertyStore store, PropertyKey key)
        {
            PropVariant pv;
            if (store.GetValue(ref key, out pv) != 0) return null;
            try
            {
                if (pv.pointerValue == IntPtr.Zero) return null;
                if (pv.vt == 31 || pv.vt == 8)
                {
                    string s = Marshal.PtrToStringUni(pv.pointerValue);
                    if (s != null) s = s.Trim();
                    if (string.IsNullOrEmpty(s)) return null;
                    return s;
                }
            }
            finally { Native.PropVariantClear(ref pv); }
            return null;
        }

        IPerChannelDbLevel FindBoost(IMMDevice dev)
        {
            IPerChannelDbLevel named = null;
            IPerChannelDbLevel gain = null;
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            try
            {
                object o;
                Guid g = IID_Topo;
                if (dev.Activate(ref g, Native.CLSCTX_INPROC, IntPtr.Zero, out o) != 0 || o == null)
                {
                    if (dev.Activate(ref g, Native.CLSCTX_ALL, IntPtr.Zero, out o) != 0 || o == null)
                        return null;
                }
                IDeviceTopology topo = o as IDeviceTopology;
                if (topo == null) { Native.Release(o); return null; }
                try
                {
                    WalkTopo(topo, seen, ref named, ref gain, 0);
                    uint cc;
                    if (topo.GetConnectorCount(out cc) == 0)
                    {
                        if (cc > 32) cc = 32;
                        for (uint i = 0; i < cc; i++)
                        {
                            IConnector c = null;
                            try
                            {
                                if (topo.GetConnector(i, out c) != 0 || c == null) continue;
                                bool connected = false;
                                c.IsConnected(out connected);
                                IConnector other = null;
                                if (connected) c.GetConnectedTo(out other);
                                IPart part = AsPart(other != null ? (object)other : (object)c);
                                if (part != null)
                                {
                                    object topoObj;
                                    if (part.GetTopologyObject(out topoObj) == 0 && topoObj != null)
                                    {
                                        IDeviceTopology adapter = topoObj as IDeviceTopology;
                                        if (adapter != null)
                                        {
                                            WalkTopo(adapter, seen, ref named, ref gain, 0);
                                            WalkAdapterConnectors(adapter, seen, ref named, ref gain);
                                        }
                                        Native.Release(topoObj);
                                    }
                                    WalkIncoming(part, seen, ref named, ref gain, 0);
                                    if (!object.ReferenceEquals(part, other) && !object.ReferenceEquals(part, c))
                                        Native.Release(part);
                                }
                                if (other != null) Native.Release(other);
                            }
                            catch { }
                            finally { if (c != null) Native.Release(c); }
                            if (named != null) break;
                        }
                    }
                }
                finally { Native.Release(topo); }
            }
            catch { }
            if (named != null)
            {
                if (gain != null && !object.ReferenceEquals(gain, named)) Native.Release(gain);
                return named;
            }
            return gain;
        }

        void WalkAdapterConnectors(IDeviceTopology adapter, Dictionary<string, bool> seen, ref IPerChannelDbLevel named, ref IPerChannelDbLevel gain)
        {
            uint acc;
            if (adapter.GetConnectorCount(out acc) != 0) return;
            if (acc > 16) acc = 16;
            for (uint k = 0; k < acc; k++)
            {
                IConnector ac = null;
                try
                {
                    if (adapter.GetConnector(k, out ac) != 0 || ac == null) continue;
                    IPart ap = AsPart(ac);
                    if (ap != null)
                    {
                        WalkIncoming(ap, seen, ref named, ref gain, 0);
                        if (!object.ReferenceEquals(ap, ac)) Native.Release(ap);
                    }
                }
                catch { }
                finally { if (ac != null) Native.Release(ac); }
            }
        }

        static IPart AsPart(object com)
        {
            if (com == null) return null;
            IPart p = com as IPart;
            if (p != null) return p;
            IntPtr unk = IntPtr.Zero;
            IntPtr pp = IntPtr.Zero;
            try
            {
                unk = Marshal.GetIUnknownForObject(com);
                Guid iid = IID_Part;
                if (Marshal.QueryInterface(unk, ref iid, out pp) != 0 || pp == IntPtr.Zero) return null;
                p = (IPart)Marshal.GetTypedObjectForIUnknown(pp, typeof(IPart));
                return p;
            }
            catch { return null; }
            finally
            {
                if (pp != IntPtr.Zero) Marshal.Release(pp);
                if (unk != IntPtr.Zero) Marshal.Release(unk);
            }
        }

        void WalkTopo(IDeviceTopology topo, Dictionary<string, bool> seen, ref IPerChannelDbLevel named, ref IPerChannelDbLevel gain, int depth)
        {
            if (topo == null || depth > 8) return;
            uint n;
            if (topo.GetSubunitCount(out n) != 0) return;
            if (n > 64) n = 64;
            for (uint i = 0; i < n; i++)
            {
                IPart part;
                if (topo.GetSubunit(i, out part) != 0 || part == null) continue;
                Consider(part, seen, ref named, ref gain);
                Native.Release(part);
            }
        }

        void WalkIncoming(IPart part, Dictionary<string, bool> seen, ref IPerChannelDbLevel named, ref IPerChannelDbLevel gain, int depth)
        {
            if (part == null || depth > 8) return;
            Consider(part, seen, ref named, ref gain);
            IPartsList list;
            if (part.EnumPartsIncoming(out list) != 0 || list == null) return;
            uint n;
            list.GetCount(out n);
            if (n > 16) n = 16;
            for (uint i = 0; i < n; i++)
            {
                IPart p;
                if (list.GetPart(i, out p) != 0 || p == null) continue;
                WalkIncoming(p, seen, ref named, ref gain, depth + 1);
                Native.Release(p);
            }
            Native.Release(list);
        }

        void WalkOutgoing(IPart part, Dictionary<string, bool> seen, ref IPerChannelDbLevel named, ref IPerChannelDbLevel gain, int depth)
        {
            if (part == null || depth > 8) return;
            Consider(part, seen, ref named, ref gain);
            IPartsList list;
            if (part.EnumPartsOutgoing(out list) != 0 || list == null) return;
            uint n;
            list.GetCount(out n);
            if (n > 16) n = 16;
            for (uint i = 0; i < n; i++)
            {
                IPart p;
                if (list.GetPart(i, out p) != 0 || p == null) continue;
                WalkOutgoing(p, seen, ref named, ref gain, depth + 1);
                Native.Release(p);
            }
            Native.Release(list);
        }

        IPerChannelDbLevel TryLevel(IPart part)
        {
            object o = null;
            Guid g = IID_Boost;
            int hr = part.Activate(Native.CLSCTX_INPROC, ref g, out o);
            if (hr != 0 || o == null)
            {
                g = IID_PerCh;
                hr = part.Activate(Native.CLSCTX_INPROC, ref g, out o);
            }
            if (hr != 0 || o == null)
            {
                g = IID_Boost;
                hr = part.Activate(Native.CLSCTX_ALL, ref g, out o);
            }
            if (hr != 0 || o == null)
            {
                g = IID_PerCh;
                hr = part.Activate(Native.CLSCTX_ALL, ref g, out o);
            }
            if (hr != 0 || o == null) return null;
            IPerChannelDbLevel p = o as IPerChannelDbLevel;
            if (p != null) return p;
            try
            {
                IntPtr unk = Marshal.GetIUnknownForObject(o);
                try
                {
                    p = (IPerChannelDbLevel)Marshal.GetTypedObjectForIUnknown(unk, typeof(IPerChannelDbLevel));
                    if (p != null) return p;
                }
                finally { Marshal.Release(unk); }
            }
            catch { }
            Native.Release(o);
            return null;
        }

        void Consider(IPart part, Dictionary<string, bool> seen, ref IPerChannelDbLevel named, ref IPerChannelDbLevel gain)
        {
            string gid = null;
            try { part.GetGlobalId(out gid); }
            catch { }
            if (gid != null && gid.Length > 0)
            {
                if (seen.ContainsKey(gid)) return;
                seen[gid] = true;
            }
            IPerChannelDbLevel vol = TryLevel(part);
            if (vol == null) return;
            uint ch = 0;
            vol.GetChannelCount(out ch);
            float min, max, step;
            if (vol.GetLevelRange(0, out min, out max, out step) != 0)
            {
                Native.Release(vol);
                return;
            }
            string name = "";
            try { part.GetName(out name); }
            catch { }
            if (name == null) name = "";
            bool isBoostName = name.IndexOf("Boost", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("AGC", StringComparison.OrdinalIgnoreCase) >= 0;
            bool typical = min >= -1.01f && max >= 6.0f && max <= 60.01f;
            if (isBoostName && named == null) named = vol;
            else if (!isBoostName && typical && gain == null) gain = vol;
            else Native.Release(vol);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_enum != null && _notify != null)
                    _enum.UnregisterEndpointNotificationCallback(_notify);
            }
            catch { }
            lock (_gate)
            {
                foreach (KeyValuePair<string, DevState> kv in _map)
                    Unbind(kv.Value, true);
                _map.Clear();
            }
            Native.Release(_enum);
            _enum = null;
        }
    }

    internal static class Settings
    {
        public static bool FilterAll;
        public static bool Startup;
        public static bool Notify;
        static string _machine;
        static Dictionary<string, Dictionary<string, string>> _ini = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static string Dir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicLock");
        }

        static string PathFile()
        {
            return Path.Combine(Dir(), "settings.ini");
        }

        public static string MachineGuid()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "");
                if (v != null) return v.ToString();
            }
            catch { }
            return Environment.MachineName;
        }

        public static void Load()
        {
            _machine = MachineGuid();
            _ini.Clear();
            FilterAll = false;
            Startup = false;
            Notify = false;
            string path = PathFile();
            if (File.Exists(path))
            {
                Parse(File.ReadAllLines(path));
                string stored = Get("Machine", "Id", "");
                if (stored.Length > 0 && !string.Equals(stored, _machine, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(path); }
                    catch { }
                    _ini.Clear();
                }
            }
            Set("Machine", "Id", _machine);
            FilterAll = false;
            Startup = Get("App", "Startup", "0") == "1";
            Notify = Get("App", "Notify", "0") == "1";
            Save();
            ApplyStartup();
        }

        public static void Save()
        {
            Set("Machine", "Id", _machine);
            Set("App", "Filter", FilterAll ? "All" : "Active");
            Set("App", "Startup", Startup ? "1" : "0");
            Set("App", "Notify", Notify ? "1" : "0");
            try
            {
                Directory.CreateDirectory(Dir());
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, Dictionary<string, string>> sec in _ini)
                {
                    sb.Append('[').Append(sec.Key).Append(']').AppendLine();
                    foreach (KeyValuePair<string, string> kv in sec.Value)
                        sb.Append(kv.Key).Append('=').Append(kv.Value).AppendLine();
                    sb.AppendLine();
                }
                File.WriteAllText(PathFile(), sb.ToString());
            }
            catch { }
        }

        public static void Hydrate(DevState d)
        {
            string s = Sec(d.Id);
            d.Lock = Get(s, "Lock", "0") == "1";
            d.HasVolTarget = Get(s, "HasVol", "0") == "1";
            d.HasMuteTarget = Get(s, "HasMute", "0") == "1";
            d.HasBoostTarget = Get(s, "HasBoost", "0") == "1";
            float v;
            if (float.TryParse(Get(s, "Volume", "0"), out v)) d.VolTarget = v;
            d.MuteTarget = Get(s, "Mute", "0") == "1";
            if (float.TryParse(Get(s, "Boost", "0"), out v)) d.BoostTarget = v;
            string n = Get(s, "Name", "");
            if (n.Length > 0) d.Name = n;
        }

        public static void SaveDevice(DevState d)
        {
            string s = Sec(d.Id);
            Set(s, "Name", d.Name ?? "");
            Set(s, "Lock", d.Lock ? "1" : "0");
            Set(s, "HasVol", d.HasVolTarget ? "1" : "0");
            Set(s, "HasMute", d.HasMuteTarget ? "1" : "0");
            Set(s, "HasBoost", d.HasBoostTarget ? "1" : "0");
            Set(s, "Volume", d.VolTarget.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Set(s, "Mute", d.MuteTarget ? "1" : "0");
            Set(s, "Boost", d.BoostTarget.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Save();
        }

        public static void ApplyStartup()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (Startup)
                    key.SetValue("MicLock", "\"" + Application.ExecutablePath + "\" --tray");
                else
                    key.DeleteValue("MicLock", false);
                key.Close();
            }
            catch { }
        }

        static string Sec(string id)
        {
            return "D:" + id.Replace("[", "(").Replace("]", ")");
        }

        static string Get(string sec, string key, string def)
        {
            Dictionary<string, string> m;
            if (!_ini.TryGetValue(sec, out m)) return def;
            string v;
            if (!m.TryGetValue(key, out v) || v == null) return def;
            return v;
        }

        static void Set(string sec, string key, string val)
        {
            Dictionary<string, string> m;
            if (!_ini.TryGetValue(sec, out m))
            {
                m = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _ini[sec] = m;
            }
            m[key] = val ?? "";
        }

        static void Parse(string[] lines)
        {
            string sec = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    sec = line.Substring(1, line.Length - 2);
                    if (!_ini.ContainsKey(sec))
                        _ini[sec] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || sec.Length == 0) continue;
                _ini[sec][line.Substring(0, eq).Trim()] = line.Substring(eq + 1);
            }
        }
    }

    internal sealed class DarkList : Panel
    {
        public event EventHandler ViewChanged;
        public DarkList()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = Col.Bg;
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkScroll(Handle);
        }
        protected override Point ScrollToControl(Control activeControl)
        {
            return DisplayRectangle.Location;
        }
        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            HideHScroll();
            if (ViewChanged != null) ViewChanged(this, EventArgs.Empty);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            HideHScroll();
            if (ViewChanged != null) ViewChanged(this, EventArgs.Empty);
        }
        public void HideHScroll()
        {
            if (!IsHandleCreated) return;
            try { Native.ShowScrollBar(Handle, Native.SB_HORZ, false); }
            catch { }
        }
        public void ResetScroll()
        {
            try
            {
                AutoScroll = false;
                AutoScrollMinSize = new Size(1, 1);
                AutoScrollPosition = new Point(0, 0);
                try { VerticalScroll.Value = 0; }
                catch { }
                AutoScroll = true;
                AutoScrollPosition = new Point(0, 0);
            }
            catch { }
            HideHScroll();
        }
        public int ItemWidth()
        {
            int w = Width - SystemInformation.VerticalScrollBarWidth - 18;
            if (w < 240) w = 240;
            return w;
        }
        public void SetContentHeight(int h)
        {
            if (h < 1) h = 1;
            for (int i = 0; i < Controls.Count; i++)
            {
                Control c = Controls[i];
                if (!c.Visible) c.SetBounds(0, 0, 1, 1);
            }
            AutoScroll = false;
            AutoScrollMinSize = new Size(1, 1);
            AutoScrollPosition = new Point(0, 0);
            int w = ClientSize.Width;
            if (w < 1) w = 1;
            AutoScrollMinSize = new Size(w, h);
            AutoScroll = true;
            AutoScrollPosition = new Point(0, 0);
            try
            {
                VerticalScroll.Value = 0;
                HorizontalScroll.Enabled = false;
                HorizontalScroll.Visible = false;
            }
            catch { }
            HideHScroll();
            if (IsHandleCreated)
            {
                try
                {
                    Native.ShowScrollBar(Handle, Native.SB_HORZ, false);
                    Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED);
                    int lp = ((ClientSize.Height & 0xFFFF) << 16) | (ClientSize.Width & 0xFFFF);
                    Native.SendMessage(Handle, 5, IntPtr.Zero, (IntPtr)lp);
                }
                catch { }
            }
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            HideHScroll();
        }
    }

    internal sealed class Spinner : Control
    {
        float _ang;
        public Spinner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(40, 40);
            BackColor = Col.Bg;
        }
        public void Step()
        {
            _ang += 18f;
            if (_ang >= 360f) _ang -= 360f;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Rectangle r = new Rectangle(4, 4, Width - 8, Height - 8);
            using (Pen dim = new Pen(Col.Track, 3f))
                g.DrawEllipse(dim, r);
            using (Pen p = new Pen(Col.Accent, 3f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, r, _ang, 250);
            }
        }
    }

    internal sealed class DarkCheck : Control
    {
        bool _on;
        public event EventHandler CheckedChanged;
        public bool Checked
        {
            get { return _on; }
            set { if (_on == value) return; _on = value; Invalidate(); }
        }
        public DarkCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Height = 22;
            Cursor = Cursors.Hand;
            ForeColor = Col.Fg;
            BackColor = Color.Transparent;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _on = !_on;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Col.Surface);
            int box = 14;
            int y = (Height - box) / 2;
            Rectangle r = new Rectangle(0, y, box, box);
            using (SolidBrush b = new SolidBrush(_on ? Col.Accent : Col.Input))
                g.FillRectangle(b, r);
            using (Pen p = new Pen(_on ? Col.Accent : Col.Border))
                g.DrawRectangle(p, r);
            if (_on)
            {
                using (Pen p = new Pen(Color.FromArgb(16, 16, 16), 1.8f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new Point[] {
                        new Point(3, y + 7), new Point(6, y + 11), new Point(11, y + 3)
                    });
                }
            }
            using (SolidBrush t = new SolidBrush(Enabled ? Col.Fg : Col.Faint))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.None;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(Text, Font, t, new Rectangle(box + 6, 0, Width - box - 6, Height), sf);
            }
        }
    }

    internal sealed class DarkSlider : Control
    {
        double _min, _max, _value;
        bool _drag;
        public event EventHandler ValueChanged;
        public event EventHandler DragEnded;
        public bool IsDragging { get { return _drag; } }
        public double Minimum { get { return _min; } set { _min = value; Invalidate(); } }
        public double Maximum { get { return _max; } set { _max = value; Invalidate(); } }
        public double Value
        {
            get { return _value; }
            set
            {
                double v = value;
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                if (Math.Abs(_value - v) < 0.0001) return;
                _value = v;
                Invalidate();
            }
        }
        public DarkSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 22;
            _min = 0;
            _max = 100;
            Cursor = Cursors.Hand;
        }
        float ThumbX()
        {
            double span = _max - _min;
            if (span <= 0) return 7;
            double t = (_value - _min) / span;
            return 7 + (float)t * (Width - 14);
        }
        void FromX(int x)
        {
            float t = (x - 7f) / Math.Max(1, Width - 14);
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            Value = _min + t * (_max - _min);
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _drag = true;
                Capture = true;
                FromX(e.X);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag) FromX(e.X);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_drag)
            {
                _drag = false;
                Capture = false;
                if (DragEnded != null) DragEnded(this, EventArgs.Empty);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Col.Surface);
            int y = Height / 2;
            Rectangle track = new Rectangle(7, y - 2, Math.Max(1, Width - 14), 4);
            using (SolidBrush b = new SolidBrush(Col.Track))
                g.FillRectangle(b, track);
            float tx = ThumbX();
            Rectangle fill = new Rectangle(7, y - 2, Math.Max(0, (int)tx - 7), 4);
            using (SolidBrush b = new SolidBrush(Enabled ? Col.Accent : Col.Faint))
                g.FillRectangle(b, fill);
            using (SolidBrush b = new SolidBrush(Enabled ? Col.Fg : Col.Faint))
                g.FillEllipse(b, tx - 6, y - 6, 12, 12);
        }
    }

    internal sealed class DarkMeter : Control
    {
        float _level, _hold;
        public float Level
        {
            get { return _level; }
            set
            {
                if (Math.Abs(_level - value) < 0.015f) return;
                _level = value;
                Invalidate();
            }
        }
        public float Hold
        {
            get { return _hold; }
            set
            {
                if (Math.Abs(_hold - value) < 0.015f) return;
                _hold = value;
                Invalidate();
            }
        }
        public DarkMeter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Width = 20;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Col.Surface);
            Rectangle well = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 10, 12)))
                g.FillRectangle(b, well);
            using (Pen p = new Pen(Col.Border))
                g.DrawRectangle(p, well);
            int segs = Math.Max(8, (Height - 4) / 5);
            int filled = (int)Math.Round(_level * segs);
            int holdSeg = (int)Math.Round(_hold * segs);
            for (int i = 0; i < segs; i++)
            {
                int fromBottom = i;
                int y = Height - 3 - (fromBottom + 1) * (Height - 4) / segs;
                int h = Math.Max(2, (Height - 4) / segs - 1);
                float t = (fromBottom + 0.5f) / segs;
                Color c;
                if (t < 0.62f) c = Color.FromArgb(34, 197, 94);
                else if (t < 0.85f) c = Color.FromArgb(245, 158, 11);
                else c = Color.FromArgb(239, 68, 68);
                bool on = fromBottom < filled || fromBottom == holdSeg - 1;
                if (!on) c = Color.FromArgb(28, 28, 30);
                using (SolidBrush b = new SolidBrush(c))
                    g.FillRectangle(b, 2, y, Width - 5, h);
            }
        }
    }

    internal sealed class DeviceCard : Panel
    {
        public DevState Dev;
        public DarkCheck LockBox;
        public DarkCheck MuteBox;
        public DarkCheck EnBox;
        public DarkSlider VolSlider;
        public TextBox VolBox;
        public DarkMeter Meter;
        public Label NameLbl;
        public Label StatusLbl;
        public Label VolLbl;
        public Label PctLbl;
        bool _ui;
        Engine _eng;

        public DeviceCard(Engine eng, DevState d)
        {
            _eng = eng;
            Dev = d;
            SuspendLayout();
            BackColor = Col.Surface;
            ForeColor = Col.Fg;
            DoubleBuffered = true;

            NameLbl = new Label();
            NameLbl.AutoSize = false;
            NameLbl.ForeColor = Col.Fg;
            NameLbl.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            NameLbl.BackColor = Color.Transparent;

            StatusLbl = new Label();
            StatusLbl.AutoSize = false;
            StatusLbl.TextAlign = ContentAlignment.MiddleLeft;
            StatusLbl.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            StatusLbl.BackColor = Color.Transparent;

            LockBox = MkCheck("Lock");
            MuteBox = MkCheck("Mute");
            EnBox = MkCheck("Enabled");

            VolLbl = MkLbl("Level");
            PctLbl = MkLbl("%");

            VolSlider = new DarkSlider();
            VolSlider.Minimum = 0;
            VolSlider.Maximum = 100;

            VolBox = MkNum();
            Meter = new DarkMeter();

            Controls.Add(NameLbl);
            Controls.Add(StatusLbl);
            Controls.Add(LockBox);
            Controls.Add(MuteBox);
            Controls.Add(EnBox);
            Controls.Add(VolLbl);
            Controls.Add(VolSlider);
            Controls.Add(VolBox);
            Controls.Add(PctLbl);
            Controls.Add(Meter);

            LockBox.CheckedChanged += delegate { if (_ui) return; _eng.UserSetLock(Dev, LockBox.Checked); };
            MuteBox.CheckedChanged += delegate { if (_ui) return; _eng.UserSetMute(Dev, MuteBox.Checked); };
            EnBox.CheckedChanged += delegate
            {
                if (_ui) return;
                _eng.UserSetEnabled(Dev, EnBox.Checked);
            };
            VolSlider.ValueChanged += delegate
            {
                if (_ui) return;
                VolBox.Text = ((int)Math.Round(VolSlider.Value)).ToString();
                if (VolSlider.IsDragging) _eng.UserSetVolume(Dev, (float)(VolSlider.Value / 100.0));
            };
            VolSlider.DragEnded += delegate { if (_ui) return; _eng.UserSetVolume(Dev, (float)(VolSlider.Value / 100.0)); };
            VolBox.Leave += delegate { CommitVol(); };
            VolBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { CommitVol(); e.SuppressKeyPress = true; } };

            Height = 136;
            LayoutControls();
            ResumeLayout(false);
            SyncFromDev(true);
        }

        public void BindTo(DevState d)
        {
            if (d == null) return;
            Dev = d;
            SyncFromDev(true);
        }

        DarkCheck MkCheck(string t)
        {
            DarkCheck c = new DarkCheck();
            c.Text = t;
            c.Font = new Font("Segoe UI", 8.5f);
            c.Width = t == "Enabled" ? 100 : (t == "Mute" ? 80 : 72);
            return c;
        }
        Label MkLbl(string t)
        {
            Label l = new Label();
            l.Text = t;
            l.ForeColor = Col.Muted;
            l.BackColor = Color.Transparent;
            l.Font = new Font("Segoe UI", 8.25f);
            l.AutoSize = false;
            return l;
        }
        TextBox MkNum()
        {
            TextBox t = new TextBox();
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Col.Input;
            t.ForeColor = Col.Fg;
            t.TextAlign = HorizontalAlignment.Center;
            t.Font = new Font("Segoe UI", 8.5f);
            t.Width = 42;
            t.Height = 22;
            return t;
        }

        void CommitVol()
        {
            if (_ui) return;
            int n;
            if (!int.TryParse(VolBox.Text.Trim(), out n)) n = (int)Math.Round(VolSlider.Value);
            if (n < 0) n = 0;
            if (n > 100) n = 100;
            _ui = true;
            VolSlider.Value = n;
            VolBox.Text = n.ToString();
            _ui = false;
            _eng.UserSetVolume(Dev, n / 100f);
        }

        public void SyncFromDev(bool force)
        {
            float vol;
            bool mute;
            float boost;
            _eng.TryGetLive(Dev, out vol, out mute, out boost);
            _ui = true;
            NameLbl.Text = Dev.Name ?? "Device";
            StatusLbl.BackColor = Color.Transparent;
            if (!Dev.Enabled)
            {
                StatusLbl.Text = "Disabled";
                StatusLbl.ForeColor = Col.Disabled;
            }
            else if ((Dev.WinState & Native.DEVICE_STATE_UNPLUGGED) != 0)
            {
                StatusLbl.Text = "Unplugged";
                StatusLbl.ForeColor = Col.Muted;
            }
            else
            {
                StatusLbl.Text = "Ready";
                StatusLbl.ForeColor = Col.Ready;
            }

            LockBox.Checked = Dev.Lock;
            EnBox.Checked = Dev.Enabled;
            if (!MuteBox.Focused) MuteBox.Checked = Dev.Lock && Dev.HasMuteTarget ? Dev.MuteTarget : mute;

            if (!VolSlider.IsDragging && !VolBox.Focused)
            {
                float show = (Dev.Lock && Dev.HasVolTarget) ? Dev.VolTarget : vol;
                VolSlider.Value = show * 100.0;
                VolBox.Text = ((int)Math.Round(show * 100)).ToString();
            }
            Height = 136;
            Meter.Level = Dev.LastPeak;
            Meter.Hold = Dev.PeakHold;
            _ui = false;
        }

        public void UpdateMeter()
        {
            if (Meter == null) return;
            Meter.Level = Dev.LastPeak;
            Meter.Hold = Dev.PeakHold;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Col.Border))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        public void LayoutControls()
        {
            if (NameLbl == null || Meter == null || VolBox == null || StatusLbl == null) return;
            int pad = 20;
            int lockPad = 15;
            int rowH = 22;
            int meterW = 20;
            int meterGap = 20;
            int meterX = Width - pad - meterW;
            int right = meterX - meterGap;
            if (right < 260) right = 260;
            int unitW = 28;
            int boxW = 42;
            int nameY = pad;
            int lockY = nameY + rowH + lockPad;
            int rowY = lockY + rowH + lockPad;
            PctLbl.SetBounds(right - unitW, rowY, unitW, 22);
            VolBox.SetBounds(right - unitW - 4 - boxW, rowY, boxW, 22);
            int stW = right - VolBox.Left;
            if (stW < 74) stW = 74;
            StatusLbl.SetBounds(VolBox.Left, nameY, stW, 22);
            NameLbl.SetBounds(pad, nameY, Math.Max(40, StatusLbl.Left - pad - 8), 22);
            LockBox.SetBounds(pad, lockY, 72, 22);
            MuteBox.SetBounds(pad + 76, lockY, 80, 22);
            EnBox.SetBounds(pad + 76 + 84, lockY, 100, 22);
            VolLbl.SetBounds(pad, rowY, 70, 22);
            int sl = pad + 70 + 4;
            int slw = VolBox.Left - sl - 8;
            if (slw < 40) slw = 40;
            VolSlider.SetBounds(sl, rowY, slw, 22);
            Meter.SetBounds(meterX, pad, meterW, Height - pad * 2);
        }
    }

    internal sealed class GithubLink : Control
    {
        static Image _icon;
        const string Url = "https://github.com/froxel/MicLock/";
        const string IconB64 = "iVBORw0KGgoAAAANSUhEUgAAAIYAAAAgCAYAAADXPABiAAAIGElEQVR42u2be4xXxRXHP78FYXEXRFdZgy0P14rYYBEfGGoLNammFh+Nj1BTqcWiEKykVgMhGBvUFCKmKNqH+KgrQrRWjYrR0FbExsgCtmgFFVDAB64FAWERWPo7/nHPldnJmfv4/X5bCuw3udnduTNn5s6cOed8z8wWRIQOdMBH5xLbDQJOAwYA9Vr2H2ANsAx4E9jbMb2HhmIcD4wGfgycmFL3feDPQCPwVsc0H3goZHAl3YEZwLVAVQl9zAV+BXzaMd0Hj2IMB+YBvcvsZxvwc+CJjik/8BVjDPBAhfu7BZhWATnHA+cA3wJ6AZ2A3UAz8DqwGNiQ0L4nMAvoBywHbgKKJYyjF3AKEE9iAVgFfJTQpg441WuzBlhXoTmu1xjQXdgmYHsuKSJiPVdK++HGQJ9ZnrNF5PkMfewVkWdF5HRDRpWIrPDqzy9xPNY83ZDS5kKjza1lzIn/jDbkD80rx4oZhmjQ6OO/JWqwz07uAC4oQc5M4BXgBxnqdgJGAkuByd67obrLXYwCDk/Z5fVG+R6jbHfK2FqNsl0VtMp7MqxBKizFeMoouxzoD1wDvOuUbwTeBlZ6pvsTDTj7AyMMeU9qUJsV96m8UvAbYKLz91ajTtFQ/OHAFOCvauavD7TLu4GKFdx0WeXnTlb5dHUK0McQ+oL6qDnAg8D5wHvAakdDC0BfzW00AVu0fB2wA6j1+r1PqW8argPGGuWbNJhdpjuuHjgPONeoO0u/4R2NAe5RuTF+aez05z0rcmjlZRy/Ui0irYZ/KopIfRk+r5uIbAnEAiektD060K5RRHoE2owMfMdyr94wEblaRE4KyFmXIXa43OhnXMo3nWe0mVzBGMMa05C8clyLcVUg4VVQFtBcou59XVmAhesDJtrdyT4Wa6IthOeAK4H5jk9/S11CjG7Aq/oAVKvV6QF0db7bZyB1Gr/sUZdUrPA+rdYxuKb/M8PVVOlY3DXaDnwRkNvF+b0B+I668vXAEnNtHS15LbA7t4tInzI0+Ehj98XYkNCuSkQ2Gm0aMvZ7m4hcKiK9jXcvichOEWnRn7O1vFHLWtRSutgjIju0/t8SGEY5FmOUynefvoaMY52xxM+EBIsRW9dHjXc7ReSekMXop9zawq9TcgJp2AL8AngmYE0agLXGu5OAY72y5YG6FqYmvDtGrUaMI/XnUQns5DB94vYhfJwyrvUJ77p544oZlsW6aryy2oDMHTrPd2vux+pzAnCGMrY2wecZnrlxA8/HK2AiF6jptVzK2YHFts5jXquQyf7cmLxYMbLgmIRof6TOZSEwn4NzUs1ioKzoscoQ5d2l7rVfyjedCdwK3OwqxoBA5Q/0qQSFWqKswcc3Am36GGWrjbLTlSlJCi3rpExodgJtf0SVdC9wqbcL/wn8S63GqgCrQxnU2P8jfnG0PjF1b9INMNHI50wB7gI2dU7ZKdsrOMBtgfJQPqM2YBatDx+UcQwDU/I5v9cHYJhnteY471z3cqDgIs+dP6hU/zRvHn4IPJx2Wnocpd/ZyOIa8mbwumSsl1c5QzGFn/0sO3G0n/BYIMazWN/JrincHBDYU1PkTRU42Bmc0d+72VNLUbNkb7Om5/OgkLHeRp3PqoAidQ+4yfbE3ED5v3VjdfEteKwYqxKETgEuLnNgSQzh3UD5+0aZpVxLNHj2U8znAtMzKFulMVXNdAgjgJfaIe2dhDWB8p0aLtT5Gy1WjKUavVYHfNNlRDeySsEwL/3s45UEbfbx7UActCzADny8/r9IJpf5Pkv9Yg4LlhRDVgXc81fm7oPA5MZ4PCXbGMJIYFHC+3WE7yFsM/xiz5hOZYgPbjTcSFM7LniMrhmym3ncVWsg/5NHMYYHyuuM4H+375/neNz3d8DTTtnDRGnmMxMi8mqHE/8JeDYlcn8y5YN+a5RNS/jQ+JsWEKWWXbxAeaeYW8qIO7KiU0Z2Nian3JDFHh2K7VzFeBRocRTjEeBHtD2tHKU+fSvwXUNoA/vy7z/NMODZKe8XAQsD5Xcp1TpCJ68v0fXBlcD3jTY355xMP3i8kOiKY532WYqvT4MViM/w/h5FlKnMg95EZ0XunZKfALcbdf8e2ce2OfJxXh59fOBW0DwRqQ2cbzyR8SbXQxnPPHomnM6KvttknG24mOHJfNV7f7/R7wZDTnxesrCdzkoaAuNfoWNclPCNEwNnJe687BCRZSKyOiBjeegG1x80u+eb8kaiW1czNSN4RSDZVASuzmCyW4DxGbV9q7KO5gRKXZdg1v8ITCph974RiF1qsG9zVQJrA8H4KTqvsQt9OYFp+PjYIQ41amVPCNQdk5QDuNgLpGLK9xzRpdm/ZEgifZRS5wLyXWdbA3wzJzNqBsbpY7k8F18z6tyQkGOJz0p65GAAbfIEAXkQ3ZZLOoaIXby/uHHa+wgj93NVSsqglejS1Iq4wMpqrleuHbOJSRqR30l0a6qrBkk7EzpKWvRrSuTxm3XShgCXaOzTXxenQHQXYZMyjwUa2LYEZN1L2zOalwP5lQHa5yCPTcS5l5VG8ijtoO8do80iL/g7megs4yJd2CLRWU0j+/4F4zGPsfzDsXRzvYBWNJ5YCvwMOEvXsRl4UYlHm4PMpH8fGKGsotYx/x+qRl6RsrirA+ZqgrKdSqFGXUlBFfUzDj4cpq652A5yW0MvO6cwgoGqpd/TRRiQkavXGL5zDNHtq0qiJcEqHCxo3R9y084ZPiS63DGWtmnztNTyLidwnE506WYxHThgkOV/VztwCOJL73C0oV0lFBIAAAAASUVORK5CYII=";
        bool _hover;
        ToolTip _tip;

        public GithubLink()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Size = new Size(134, 32);
            BackColor = Col.Bg;
            _tip = new ToolTip();
            _tip.ShowAlways = true;
            _tip.SetToolTip(this, "Go to the project on GitHub");
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
            if (_icon == null)
            {
                try
                {
                    byte[] raw = Convert.FromBase64String(IconB64);
                    using (MemoryStream ms = new MemoryStream(raw))
                    using (Image tmp = Image.FromStream(ms))
                        _icon = new Bitmap(tmp);
                }
                catch { }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            try { Process.Start(Url); }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(BackColor);
            if (_icon == null) return;
            float op = _hover ? 1f : 0.7f;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ColorMatrix cm = new ColorMatrix();
                cm.Matrix33 = op;
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(_icon, new Rectangle(0, 0, Width, Height), 0, 0, _icon.Width, _icon.Height, GraphicsUnit.Pixel, ia);
            }
        }
    }

    internal sealed class MainForm : Form
    {
        Engine _eng;
        NotifyIcon _tray;
        ContextMenuStrip _menu;
        DarkList _list;
        ComboBox _filter;
        Button _openSound;
        DarkCheck _startup;
        DarkCheck _notify;
        Label _filterLbl;
        Label _notifyHelp;
        Label _startupHelp;
        Label _count;
        Label _empty;
        Panel _load;
        Label _loadLbl;
        Spinner _spin;
        System.Windows.Forms.Timer _timer;
        bool _exit;
        bool _metersOn;
        bool _ready;
        int _tick;
        Dictionary<string, DeviceCard> _byId = new Dictionary<string, DeviceCard>(StringComparer.OrdinalIgnoreCase);
        List<DeviceCard> _shown = new List<DeviceCard>();
        GithubLink _github;
        Panel _footer;
        Font _uiFont;

        public MainForm()
        {
            Settings.Load();
            _uiFont = new Font("Segoe UI", 9f);
            Font = _uiFont;
            Text = "MicLock";
            BackColor = Col.Bg;
            ForeColor = Col.Fg;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(730, 420);
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered = true;
            ShowInTaskbar = true;
            try
            {
                string ico = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "app.ico");
                if (File.Exists(ico)) Icon = new Icon(ico);
                else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            try
            {
                _eng = new Engine();
                _eng.Restored += OnRestored;
                _eng.DevicesChanged += delegate
                {
                    if (IsHandleCreated) BeginInvoke(new MethodInvoker(RefreshShown));
                    else RefreshShown();
                };
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                _eng = new Engine();
            }

            BuildChrome();
            BuildTray();
            Size = new Size(760, 640);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 50;
            _timer.Tick += OnTick;
            _timer.Start();

            Load += delegate
            {
                _eng.AttachUi();
                if (Settings.Startup) Settings.ApplyStartup();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle);
            Native.SetWindowTheme(Handle, "DarkMode_Explorer", null);
            if (_list != null && _list.IsHandleCreated) Native.DarkScroll(_list.Handle);
            if (_filter != null && _filter.IsHandleCreated)
                Native.SetWindowTheme(_filter.Handle, "DarkMode_Explorer", null);
        }

        void BuildChrome()
        {
            _filterLbl = new Label();
            _filterLbl.Text = "Filter";
            _filterLbl.ForeColor = Col.Muted;
            _filterLbl.BackColor = Color.Transparent;
            _filterLbl.Font = new Font("Segoe UI", 8.5f);
            _filterLbl.AutoSize = false;

            _filter = new ComboBox();
            _filter.DropDownStyle = ComboBoxStyle.DropDownList;
            _filter.FlatStyle = FlatStyle.Flat;
            _filter.DrawMode = DrawMode.OwnerDrawFixed;
            _filter.ItemHeight = 18;
            _filter.BackColor = Col.Surface;
            _filter.ForeColor = Col.Fg;
            _filter.Items.Add("Active devices");
            _filter.Items.Add("All devices");
            _filter.SelectedIndex = 0;
            Settings.FilterAll = false;
            _filter.DrawItem += DrawFilterItem;
            _filter.SelectedIndexChanged += delegate
            {
                Settings.FilterAll = _filter.SelectedIndex == 1;
                Settings.Save();
                _filterSwitch = true;
                RebuildCards(false);
            };

            _count = new Label();
            _count.ForeColor = Col.Faint;
            _count.BackColor = Color.Transparent;
            _count.Font = new Font("Segoe UI", 8f);
            _count.TextAlign = ContentAlignment.MiddleLeft;

            _openSound = new Button();
            _openSound.Text = "Open Recording Devices";
            _openSound.FlatStyle = FlatStyle.Flat;
            _openSound.BackColor = Col.Elevated;
            _openSound.ForeColor = Col.Fg;
            _openSound.FlatAppearance.BorderColor = Col.Border;
            _openSound.Font = new Font("Segoe UI", 8.25f);
            _openSound.Click += delegate
            {
                try { Process.Start("control.exe", "mmsys.cpl,,1"); }
                catch
                {
                    try { Process.Start("rundll32.exe", "shell32.dll,Control_RunDLL mmsys.cpl,,1"); }
                    catch { }
                }
            };

            _list = new DarkList();
            _list.BackColor = Col.Bg;
            _list.AutoScroll = true;

            _empty = new Label();
            _empty.Text = "";
            _empty.ForeColor = Col.Muted;
            _empty.BackColor = Col.Bg;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.Font = new Font("Segoe UI", 9.5f);
            _empty.Visible = false;

            _load = new Panel();
            _load.BackColor = Col.Bg;
            _loadLbl = new Label();
            _loadLbl.Text = "Loading Devices, Please Wait";
            _loadLbl.ForeColor = Col.Fg;
            _loadLbl.BackColor = Col.Bg;
            _loadLbl.TextAlign = ContentAlignment.MiddleCenter;
            _loadLbl.Font = new Font("Segoe UI", 11f);
            _spin = new Spinner();
            _spin.BackColor = Col.Bg;
            _load.Controls.Add(_spin);
            _load.Controls.Add(_loadLbl);

            _startup = new DarkCheck();
            _startup.Text = "Run at Windows startup";
            _startup.Font = _uiFont;
            _startup.Checked = Settings.Startup;
            _startup.CheckedChanged += delegate
            {
                Settings.Startup = _startup.Checked;
                Settings.ApplyStartup();
                Settings.Save();
            };

            _startupHelp = new Label();
            _startupHelp.Text = "(Works for the current user only)";
            _startupHelp.ForeColor = Col.Muted;
            _startupHelp.BackColor = Color.Transparent;
            _startupHelp.Font = new Font("Segoe UI", 8f);
            _startupHelp.AutoSize = false;

            _notify = new DarkCheck();
            _notify.Text = "Change notification";
            _notify.Font = _uiFont;
            _notify.Checked = Settings.Notify;
            _notify.CheckedChanged += delegate
            {
                Settings.Notify = _notify.Checked;
                Settings.Save();
            };

            _notifyHelp = new Label();
            _notifyHelp.Text = "(Notifies you when another app tries to change the level)";
            _notifyHelp.ForeColor = Col.Muted;
            _notifyHelp.BackColor = Color.Transparent;
            _notifyHelp.Font = new Font("Segoe UI", 8f);
            _notifyHelp.AutoSize = false;

            _github = new GithubLink();
            _github.BackColor = Col.Bg;

            _footer = new Panel();
            _footer.BackColor = Col.Bg;
            _footer.Paint += delegate(object s, PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(Col.Faint))
                    e.Graphics.FillRectangle(b, 0, 0, _footer.Width, 1);
            };
            _footer.Controls.Add(_startup);
            _footer.Controls.Add(_startupHelp);
            _footer.Controls.Add(_notify);
            _footer.Controls.Add(_notifyHelp);
            _footer.Controls.Add(_github);

            Controls.Add(_filterLbl);
            Controls.Add(_filter);
            Controls.Add(_count);
            Controls.Add(_openSound);
            Controls.Add(_list);
            Controls.Add(_empty);
            Controls.Add(_load);
            Controls.Add(_footer);
            _load.BringToFront();
        }

        void DrawFilterItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(sel ? Col.Elevated : Col.Surface))
                e.Graphics.FillRectangle(b, e.Bounds);
            string t = _filter.Items[e.Index].ToString();
            using (SolidBrush f = new SolidBrush(Col.Fg))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(t, e.Font, f, new RectangleF(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height), sf);
            }
        }

        void BuildTray()
        {
            _menu = new ContextMenuStrip();
            _menu.BackColor = Col.Surface;
            _menu.ForeColor = Col.Fg;
            _menu.Renderer = new DarkMenuRenderer();
            ToolStripMenuItem show = new ToolStripMenuItem("Show MicLock");
            show.Click += delegate { ShowWindow(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { RealExit(); };
            _menu.Items.Add(show);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exit);

            _tray = new NotifyIcon();
            _tray.Text = "MicLock";
            try
            {
                if (Icon != null) _tray.Icon = (Icon)Icon.Clone();
                else _tray.Icon = SystemIcons.Application;
            }
            catch
            {
                _tray.Icon = SystemIcons.Application;
            }
            _tray.ContextMenuStrip = _menu;
            _tray.DoubleClick += delegate { ShowWindow(); };
            _tray.Visible = true;
        }

        public void ShowWindow()
        {
            ShowInTaskbar = true;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Show();
            Activate();
            BringToFront();
            SetMeters(true);
        }

        public void GoToTray()
        {
            SetMeters(false);
            Hide();
            ShowInTaskbar = false;
        }

        void SetMeters(bool on)
        {
            if (_eng == null) return;
            if (_metersOn == on) return;
            _metersOn = on;
            try { _eng.SetMetersActive(on); }
            catch (Exception ex) { Program.Log(ex); }
        }

        string _listSig = "";
        bool _filterSwitch;

        void RefreshShown()
        {
            if (_eng == null) return;
            try { _eng.List(true, true); }
            catch (Exception ex) { Program.Log(ex); }
            for (int i = 0; i < _shown.Count; i++)
                _shown[i].SyncFromDev(false);
        }

        void RebuildCards()
        {
            RebuildCards(true);
        }

        void RebuildCards(bool refreshDevices)
        {
            if (_eng == null || _list == null) return;
            List<DevState> devices;
            try { devices = _eng.List(Settings.FilterAll, refreshDevices); }
            catch (Exception ex)
            {
                Program.Log(ex);
                return;
            }
            StringBuilder sig = new StringBuilder();
            for (int i = 0; i < devices.Count; i++)
                sig.Append(devices[i].Id).Append('#').Append(devices[i].WinState).Append(';');
            string nextSig = sig.ToString();
            bool same = nextSig == _listSig && _shown.Count == devices.Count;
            _listSig = nextSig;
            if (_filterSwitch)
            {
                _list.ResetScroll();
                _filterSwitch = false;
                same = false;
            }
            Dictionary<string, bool> want = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            List<DeviceCard> next = new List<DeviceCard>();
            _list.SuspendLayout();
            for (int i = 0; i < devices.Count; i++)
            {
                DevState d = devices[i];
                want[d.Id] = true;
                DeviceCard card;
                if (!_byId.TryGetValue(d.Id, out card))
                {
                    try
                    {
                        card = new DeviceCard(_eng, d);
                        _byId[d.Id] = card;
                    }
                    catch (Exception ex)
                    {
                        Program.Log(ex);
                        continue;
                    }
                }
                else
                    card.Dev = d;
                if (!_list.Controls.Contains(card))
                    _list.Controls.Add(card);
                card.Visible = true;
                next.Add(card);
            }
            List<string> drop = new List<string>();
            foreach (KeyValuePair<string, DeviceCard> kv in _byId)
            {
                if (want.ContainsKey(kv.Key)) continue;
                if (_list.Controls.Contains(kv.Value))
                    _list.Controls.Remove(kv.Value);
                kv.Value.Visible = false;
                kv.Value.SetBounds(0, 0, 1, 1);
                if (Settings.FilterAll)
                {
                    drop.Add(kv.Key);
                    kv.Value.Dispose();
                }
            }
            for (int i = 0; i < drop.Count; i++) _byId.Remove(drop[i]);
            _shown = next;
            LayoutCards();
            _list.ResumeLayout(true);
            _list.SetContentHeight(4 + _shown.Count * 144 + 4);
            int vis = _shown.Count;
            int tot = _eng.TotalCount();
            _count.Text = vis.ToString() + " of " + tot.ToString() + " devices";
            if (_ready)
            {
                if (vis == 0)
                {
                    _empty.Text = !string.IsNullOrEmpty(_eng.LastError)
                        ? _eng.LastError
                        : (Settings.FilterAll ? "No recording devices found." : "No active recording devices. Switch filter to All devices.");
                    _empty.Visible = true;
                    _empty.BringToFront();
                }
                else _empty.Visible = false;
            }
        }

        void LayoutCards()
        {
            if (_list == null) return;
            int y = 4;
            int w = _list.ItemWidth();
            for (int i = 0; i < _shown.Count; i++)
            {
                DeviceCard c = _shown[i];
                if (!c.Visible) c.Visible = true;
                c.SetBounds(8, y, w, 136);
                c.LayoutControls();
                y += 144;
            }
            _list.SetContentHeight(y + 4);
            _list.HideHScroll();
        }

        void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tick++;
                if (_eng == null) return;
                _eng.AttachUi();
                if (!_ready)
                {
                    if (_spin != null) _spin.Step();
                    if (_tick >= 2)
                    {
                        _ready = true;
                        RebuildCards(true);
                        if (_load != null) _load.Visible = false;
                    }
                    return;
                }
                if (_eng.ConsumeRefresh())
                    RefreshShown();
                _eng.TickMeters();
                _eng.EnforceAll(Settings.Notify);
                for (int i = 0; i < _shown.Count; i++)
                {
                    if ((_tick % 12) == 0) _shown[i].SyncFromDev(false);
                    else _shown[i].UpdateMeter();
                }
                if (_list != null) _list.HideHScroll();
            }
            catch (Exception ex)
            {
                Program.Log(ex);
            }
        }

        void OnRestored(string name)
        {
            if (!Settings.Notify) return;
            try
            {
                _tray.BalloonTipTitle = "MicLock";
                _tray.BalloonTipText = "Volume Restored";
                _tray.ShowBalloonTip(2500);
            }
            catch { }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_filterLbl == null) return;
            int pad = 14;
            int footH = 70;
            int ghW = 134;
            int ghH = 32;
            _filterLbl.SetBounds(pad, 14, 40, 24);
            _filter.SetBounds(56, 12, 170, 24);
            _count.SetBounds(234, 14, 140, 24);
            _openSound.SetBounds(Width - pad - 16 - 188, 11, 188, 28);
            int listH = ClientSize.Height - 48 - footH;
            if (listH < 80) listH = 80;
            _list.SetBounds(0, 48, ClientSize.Width, listH);
            if (_footer != null)
                _footer.SetBounds(0, 48 + listH, ClientSize.Width, ClientSize.Height - 48 - listH);
            if (_github != null && _footer != null)
                _github.SetBounds(_footer.Width - pad - ghW, Math.Max(0, (_footer.Height - ghH) / 2), ghW, ghH);
            int helpRight = pad + ghW + 8;
            _startup.SetBounds(pad, 10, 190, 22);
            if (_startupHelp != null)
                _startupHelp.SetBounds(pad + 190, 10, Math.Max(40, (_footer != null ? _footer.Width : ClientSize.Width) - pad - 190 - helpRight), 22);
            _notify.SetBounds(pad, 38, 168, 22);
            if (_notifyHelp != null)
                _notifyHelp.SetBounds(pad + 168, 38, Math.Max(40, (_footer != null ? _footer.Width : ClientSize.Width) - pad - 168 - helpRight), 22);
            if (_empty != null) _empty.SetBounds(0, 48, ClientSize.Width, listH);
            if (_load != null)
            {
                _load.SetBounds(0, 48, ClientSize.Width, listH);
                if (_spin != null) _spin.SetBounds((_load.Width - 40) / 2, Math.Max(20, (_load.Height / 2) - 50), 40, 40);
                if (_loadLbl != null) _loadLbl.SetBounds(20, _spin.Bottom + 12, _load.Width - 40, 32);
            }
            LayoutCards();
            if (_list != null) _list.HideHScroll();
        }

        protected override void WndProc(ref Message m)
        {
            if (Program.WM_SHOWME != 0 && m.Msg == (int)Program.WM_SHOWME)
            {
                ShowWindow();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                GoToTray();
                return;
            }
            base.OnFormClosing(e);
        }

        void RealExit()
        {
            _exit = true;
            try { if (_timer != null) _timer.Stop(); }
            catch { }
            SetMeters(false);
            try { if (_eng != null) _eng.Dispose(); }
            catch { }
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }
            }
            catch { }
            Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null) _timer.Dispose();
                if (_eng != null) _eng.Dispose();
                if (_tray != null) _tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Col.Fg;
            base.OnRenderItemText(e);
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Col.Surface; } }
        public override Color MenuStripGradientEnd { get { return Col.Surface; } }
        public override Color MenuBorder { get { return Col.Border; } }
        public override Color MenuItemBorder { get { return Col.Accent; } }
        public override Color MenuItemSelected { get { return Col.Elevated; } }
        public override Color ToolStripDropDownBackground { get { return Col.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Col.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Col.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Col.Surface; } }
        public override Color SeparatorDark { get { return Col.Border; } }
        public override Color SeparatorLight { get { return Col.Border; } }
    }
}
