using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ============================================================================================
// Win32 interfaces.
//
namespace MouseUnSnag
{
    public static class StaticStuff
    {
        public const int WH_MOUSE_LL = 14; // Win32 low-level mouse event hook ID.
        public const int WM_MOUSEMOVE = 0x0200;

        public delegate IntPtr HookProc(int nCode, uint wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [
            return: MarshalAs(UnmanagedType.Bool)
        ]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, uint wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
        public static bool SetCursorPos(Point p) { return SetCursorPos(p.X, p.Y); }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);
        //public static Point? GetCursorPos() => GetCursorPos(out Point P) ? P : null;

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibrary(string fileName);

        public delegate bool ConsoleEventDelegate(int eventType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        public enum PROCESS_DPI_AWARENESS
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
        }

        [DllImport("SHCore.dll", SetLastError = true)]
        public static extern bool SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public Point pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public enum DpiType
        {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
        [DllImport("User32.dll")]
        public static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx
        [DllImport("Shcore.dll")]
        public static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

        public static uint GetDpi(Screen screen, DpiType dpiType)
        {
            try
            {
                var mon = MonitorFromPoint(screen.Bounds.Location, 2 /*MONITOR_DEFAULTTONEAREST*/ );
                GetDpiForMonitor(mon, dpiType, out uint dpiX, out uint dpiY);
                return dpiX;
            }
            catch (System.DllNotFoundException)
            {
                return 96; // On Windows <8, just assume scaling 100%.
            }
        }

        // ============================================================================================
        // ============================================================================================
        // Geometric helpers. These all deal with Rectangles, Points, and X and Y values.
        //

        // Return the signs of X and Y. This essentially gives us the "component direction" of
        // the point (N.B. the vector length is not "normalized" to a length 1 "unit vector" if
        // both the X and Y components are non-zero).
        public static Point Sign(Point p) => new Point(Math.Sign(p.X), Math.Sign(p.Y));

        // 3-way Max()
        public static int Max(int x, int y, int z) => Math.Max(x, Math.Max(y, z));

        // "Direction" vector from P1 to P2. X/Y of returned point will have values
        // of -1, 0, or 1 only (vector is not normalized to length 1).
        public static Point Direction(Point P1, Point P2) => Sign(P2 - (Size)P1);

        // If P is anywhere inside R, then OutsideDistance() returns (0,0).
        // Otherwise, it returns the (x,y) delta (sign is preserved) from P to the
        // nearest edge/corner of R. For Right and Bottom we must correct by 1,
        // since the Rectangle Right and Bottom are one larger than the largest
        // valid pixel.
        public static int OutsideXDistance(Rectangle R, Point P)
            => Math.Max(Math.Min(0, P.X - R.Left), P.X - R.Right + 1);

        public static int OutsideYDistance(Rectangle R, Point P)
            => Math.Max(Math.Min(0, P.Y - R.Top), P.Y - R.Bottom + 1);

        public static Point OutsideDistance(Rectangle R, Point P)
            => new Point(OutsideXDistance(R, P), OutsideYDistance(R, P));

        // This is sort-of the "opposite" of above. In a sense it "captures" the point to the
        // boundary/inside of the rectangle, rather than "excluding" it to the exterior of the rectangle.
        //
        // If the point is outside the rectangle, then it returns the closest location on the
        // rectangle boundary to the Point. If Point is inside Rectangle, then it just returns
        // the point.
        public static Point ClosestBoundaryPoint(this Rectangle R, Point P)
            => new Point(
                Math.Max(Math.Min(P.X, R.Right - 1), R.Left),
                Math.Max(Math.Min(P.Y, R.Bottom - 1), R.Top));

        // In which direction(s) is(are) the point outside of the rectangle? If P is
        // inside R, then this returns (0,0). Else X and/or Y can be either -1 or
        // +1, depending on which direction P is outside R.
        public static Point OutsideDirection(Rectangle R, Point P) => Sign(OutsideDistance(R, P));

        // Return TRUE if the Y value of P is within the Rectangle's Y bounds.
        public static bool ContainsY(Rectangle R, Point P) => (P.Y >= R.Top) && (P.Y < R.Bottom);

        public static bool ContainsX(Rectangle R, Point P) => (P.X >= R.Left) && (P.X < R.Right);
    }
}
