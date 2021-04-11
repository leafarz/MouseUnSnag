using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static MouseUnSnag.StaticStuff;

// =======================================================================================================
// =======================================================================================================
// 
// The MouseUnSnag class deals with the low-level mouse events.
//
//
namespace MouseUnSnag
{
    public class MouseUnSnag
    {
        public bool EnableWrap { get; set; }

        private IntPtr LLMouse_hookhand = IntPtr.Zero;
        private Point LastMouse = new Point(0, 0);
        IntPtr ThisModHandle = IntPtr.Zero;
        int NJumps = 0;

        private IntPtr SetHook(int HookNum, HookProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                if (ThisModHandle == IntPtr.Zero)
                    ThisModHandle = GetModuleHandle(curModule.ModuleName);
                return SetWindowsHookEx(HookNum, proc, ThisModHandle, 0);
            }
        }

        private void UnsetHook(ref IntPtr hookHand)
        {
            if (hookHand == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(hookHand);
            hookHand = IntPtr.Zero;
        }

        // CheckJumpCursor() returns TRUE, ONLY if the cursor is "stuck". By "stuck" we
        // specifically mean that the user is trying to move the mouse beyond the boundaries of
        // the screen currently containing the cursor. This is determined when the *current*
        // cursor position does not equal the *previous* mouse position. If there is another
        // adjacent screen (or a "wrap around" screen), then we can consider moving the mouse
        // onto that screen.
        //
        // Note that this is ENTIRELY a *GEOMETRIC* method. Screens are "rectangles", and the
        // cursor and mouse are "points." The mouse/cursor hardware interaction (obtaining
        // current mouse and cursor information) is handled in routines further below, and any
        // Screen changes are handled by the DisplaySettingsChanged event. There are no
        // hardware or OS/Win32 references or interactions here.
        bool CheckJumpCursor(Point mouse, Point cursor, out Point NewCursor)
        {
            NewCursor = cursor; // Default is to not move cursor.

            // Gather pertinent information about cursor, mouse, screens.
            Point Dir = Direction(cursor, mouse);
            SnagScreen cursorScreen = SnagScreen.WhichScreen(cursor);
            SnagScreen mouseScreen = SnagScreen.WhichScreen(mouse);
            bool IsStuck = (cursor != LastMouse) && (mouseScreen != cursorScreen);
            Point StuckDirection = OutsideDirection(cursorScreen.R, mouse);

            string StuckString = IsStuck ? "--STUCK--" : "         ";

            //        Console.Write ($" FarOut{StuckDirection}/{OutsideDis//tance(cursorScreen.R, mouse)} " +
            //            $"mouse:{mouse}  cursor:{cursor} (OnMon#{cursorScreen}/{mouseScreen}) last:{LastMouse}  " +
            //            $"#UnSnags {NJumps}   {StuckString}        \r");

            Console.Write($" StuckDirection/Distance{StuckDirection}/{OutsideDistance(cursorScreen.R, mouse)} " +
                $"cur_mouse:{mouse}  prev_mouse:{LastMouse} ==? cursor:{cursor} (OnMon#{cursorScreen}/{mouseScreen})  " +
                $"#UnSnags {NJumps}   {StuckString}   \r");

            LastMouse = mouse;

            // Let caller know we did NOT jump the cursor.
            if (!IsStuck)
                return false;

            SnagScreen jumpScreen = SnagScreen.ScreenInDirection(StuckDirection, cursorScreen.R);

            // If the mouse "location" (which can take on a value beyond the current
            // cursor screen) has a value, then it is "within" another valid screen
            // bounds, so just jump to it!
            if (EnableWrap && mouseScreen != null)
            {
                NewCursor = mouse;
            }
            else if (jumpScreen != null)
            {
                NewCursor = jumpScreen.R.ClosestBoundaryPoint(cursor);
            }
            else if (EnableWrap && StuckDirection.X != 0)
            {
                NewCursor = SnagScreen.WrapPoint(StuckDirection, cursor);
            }
            else
                return false;

            ++NJumps;
            Console.Write($"\n -- JUMPED!!! --\n");
            return true;
        }

        // Called whenever the mouse moves. This routine leans entirely on the
        // CheckJumpCursor() routine to see if there is any need to "mess with" the cursor
        // position, to make it jump from one monitor to another.
        private IntPtr LLMouseHookCallback(int nCode, uint wParam, IntPtr lParam)
        {
            if ((nCode < 0) || (wParam != WM_MOUSEMOVE) || UpdatingDisplaySettings)
                goto ExitToNextHook;

            var hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            Point mouse = hookStruct.pt;

            // If we jump the cursor, then we return 1 here to tell the OS that we
            // have handled the message, so it doesn't call SetCursorPos() right
            // after we do, and "undo" our call to SetCursorPos().
            if (GetCursorPos(out Point cursor) && CheckJumpCursor(mouse, cursor, out Point NewCursor))
            {
                SetCursorPos(NewCursor);
                return (IntPtr)1;
            }

        // Default is to let the OS handle the mouse events, when "return" does not happen in
        // if() clause above.
        ExitToNextHook:
            return CallNextHookEx(LLMouse_hookhand, nCode, wParam, lParam);
        }

        bool UpdatingDisplaySettings = false;
        void Event_DisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdatingDisplaySettings = true;
            //Console.WriteLine("\nDisplay Settings Changed...");
            //ShowScreens ();
            SnagScreen.Init(Screen.AllScreens);
            SnagScreen.ShowAll();
            UpdatingDisplaySettings = false;
        }

        // Need to explicitly keep a reference to this, so it does not get "garbage collected."
        private HookProc MouseHookDelegate = null;

        // Catch program CTRL-C termination, and unhook the mouse event.
        private ConsoleEventDelegate CTRL_C_handler;
        private bool ConsoleEventCallback(int eventType)
        {
            Console.Write("\nIn ConsoleEventCallback, Unhooking mouse events...");
            UnsetHook(ref LLMouse_hookhand);
            SystemEvents.DisplaySettingsChanged -= Event_DisplaySettingsChanged;
            //Console.WriteLine("  Done.");
            return false;
        }

        public void Run(string[] args)
        {
            // DPI Awareness API is not available on older OS's, but they work in
            // physical pixels anyway, so we just ignore if the call fails.
            try
            {
                SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
            }
            catch (System.DllNotFoundException)
            {
                //Console.WriteLine("No SHCore.DLL. No problem.");
            }

            // Make sure we catch CTRL-C hard-exit of program.
            CTRL_C_handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CTRL_C_handler, true);

            //ShowScreens ();
            SnagScreen.Init(Screen.AllScreens);
            SnagScreen.ShowAll();

            // Get notified of any screen configuration changes.
            SystemEvents.DisplaySettingsChanged += Event_DisplaySettingsChanged;

            //ShowWindow(GetConsoleWindow(), SW_HIDE);

            // Keep a reference to the delegate, so it does not get garbage collected.
            MouseHookDelegate = LLMouseHookCallback;
            LLMouse_hookhand = SetHook(WH_MOUSE_LL, MouseHookDelegate);

            //Console.WriteLine("");

            // This is the one that runs "forever" while the application is alive, and handles
            // events, etc. This application is ABSOLUTELY ENTIRELY driven by the LLMouseHook
            // and DisplaySettingsChanged events.
            Application.Run();

            //Console.WriteLine("Exiting!!!");
            UnsetHook(ref LLMouse_hookhand);
        }
    }
}
