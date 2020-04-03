using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows;

namespace DotnetHook
{
    #region DLLImport
    public static class DLLImport
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        public static extern ushort GetAsyncKeyState(Int32 vKey);
        // 같이 눌린 키를 확인할 때 사용. 복합키 (CTRL + A 같은) 일때 사용.
        // C# Keyboard.GetKeyStates 로 대체 가능합니다.

        [DllImport("user32.dll", EntryPoint = "FindWindowEx")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        // 프로그램(또는 프로세스)의 핸들러값을 찾는다.

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
        // 프로그램(또는 프로세스)에 내가 원하는 값을 바로 보낸다.

        [DllImport("user32.dll", CharSet = CharSet.Auto)] // used for button-down & button-up
        public static extern int PostMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
        // 프로그램(또는 프로세스)에 내가 원하는 값을 윈도우 메시지큐에 쌓는다. 큐에 쌓인 값은 순차적으로 나가게 된다.

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        // 프로그램을 활성화 시킨다. 
    }
    #endregion

    public static class HookKeyBoard
    {
        private static LowLevelProc _proc = HookCallbackInner;
        private static IntPtr hookId = IntPtr.Zero;
        /// <summary>
        /// 장치를 후킹한 후 원래 전달되어야 하는 프로그램으로 정보를 전달 해야하는지의 여부
        /// </summary>
        public static bool KeyIgnore { set; get; }
        /// <summary>
        /// 후킹을 시작 했는 지의 여부
        /// </summary>
        public static bool NowHooking { private set; get; }

        private static IntPtr HookCallbackInner(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)Constants.Enum.WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    KeyDown?.Invoke(null, new RawKeyEventArgs(vkCode, false));
                }
                else if (wParam == (IntPtr)Constants.Enum.WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    KeyUp?.Invoke(null, new RawKeyEventArgs(vkCode, false));
                }
            }
            if (KeyIgnore)
                return (IntPtr)1;
            return DLLImport.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public static IntPtr SetHook(LowLevelProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return DLLImport.SetWindowsHookEx((int)Constants.Enum.WH_KEYBOARD_LL, proc,
                    DLLImport.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public static event RawKeyEventHandler KeyDown;
        public static event RawKeyEventHandler KeyUp;

        public static void Start()
        {
            if (hookId != IntPtr.Zero)
            {
                Stop();
                NowHooking = false;
                Start();
            }
            hookId = SetHook(_proc);
            NowHooking = true;
        }

        public static void Stop()
        {
            DLLImport.UnhookWindowsHookEx(hookId);
            NowHooking = false;
        }
    }

    public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate void RawKeyEventHandler(object sender, RawKeyEventArgs args);
    public delegate void RawMouseEventHandler(object sender, RawMouseEventArgs args);

    internal struct Constants
    {
        public enum Enum : int
        {
            VK_PROCESSKEY = 0xE5,
            WM_IME_COMPOSITION = 0x10F,
            WM_IME_ENDCOMPOSITION = 0x10E,
            KEYEVENTF_EXTENDEDKEY = 0x1,
            KEYEVENTF_KEYUP = 0x2,
            WH_KEYBOARD_LL = 0xD,
            WM_KEYDOWN = 0x100,
            WM_KEYUP = 0x101,
            WM_CHAR = 0x105,
            WM_IME_STARTCOMPOSITION = 0x010D,
            WM_MOUSEMOVE = 0x0200,
            WH_MOUSE_LL = 0xE,
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_RBUTTONDBLCLK = 0x0206,
            WM_MOUSEWHEEL = 0x020A
        }
    }

    public class RawKeyEventArgs : EventArgs
    {
        public int VKCode;
        public Key Key;
        public bool IsSysKey;
        public RawKeyEventArgs(int VKCode, bool isSysKey)
        {
            this.VKCode = VKCode;
            this.IsSysKey = isSysKey;
            this.Key = KeyInterop.KeyFromVirtualKey(VKCode);
        }
    }

    public class RawMouseEventArgs : EventArgs
    {
        public Point Position;
        public MouseButton Button;
        public int ClickCount;
        public short Wheel;
        public RawMouseEventArgs(Point position, MouseButton button, int clickcount, short wheel)
        {
            this.Position = position;
            this.Button = button;
            this.ClickCount = clickcount;
            this.Wheel = wheel;
        }
    }


    public static class HookMouse
    {
        private static LowLevelProc _proc = HookCallbackInner;
        private static IntPtr hookId = IntPtr.Zero;
        /// <summary>
        /// 장치를 후킹한 후 원래 전달되어야 하는 프로그램으로 정보를 전달 해야하는지의 여부
        /// </summary>
        public static bool KeyIgnore { set; get; }
        /// <summary>
        /// 후킹을 시작 했는 지의 여부
        /// </summary>
        public static bool NowHooking { private set; get; }

        public static void Start()
        {
            if (hookId != IntPtr.Zero)
            {
                Stop();
                NowHooking = false;
                Start();
            }
            hookId = SetHook(_proc);
            NowHooking = true;
        }

        public static void Stop()
        {
            DLLImport.UnhookWindowsHookEx(hookId);
            NowHooking = false;
        }

        public static event RawMouseEventHandler MouseDown;
        public static event RawMouseEventHandler MouseUp;
        public static event RawMouseEventHandler MouseClick;
        public static event RawMouseEventHandler MouseDoubleClick;
        public static event RawMouseEventHandler MouseWheel;
        public static event RawMouseEventHandler MouseMove;

        private static IntPtr SetHook(LowLevelProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return DLLImport.SetWindowsHookEx((int)Constants.Enum.WH_MOUSE_LL, proc,
                    DLLImport.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static int m_OldX;
        private static int m_OldY;

        private static IntPtr HookCallbackInner(int nCode, IntPtr wParam, IntPtr lParam)  //////////여기를 개조 해야함
        {
            if (nCode >= 0)
            {
                //Marshall the data from callback.
                MouseLLHookStruct mouseHookStruct = (MouseLLHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseLLHookStruct));

                //detect button clicked
                MouseButton? button = null;
                short mouseDelta = 0;
                int clickCount = 0;
                bool mouseDown = false;
                bool mouseUp = false;

                Constants.Enum WParam = (Constants.Enum)wParam.ToInt32();
                switch (WParam)
                {
                    case Constants.Enum.WM_LBUTTONDOWN:
                        mouseDown = true;
                        button = MouseButton.Left;
                        clickCount = 1;
                        break;
                    case Constants.Enum.WM_LBUTTONUP:
                        mouseUp = true;
                        button = MouseButton.Left;
                        clickCount = 1;
                        break;
                    case Constants.Enum.WM_LBUTTONDBLCLK:
                        button = MouseButton.Left;
                        clickCount = 2;
                        break;
                    case Constants.Enum.WM_RBUTTONDOWN:
                        mouseDown = true;
                        button = MouseButton.Right;
                        clickCount = 1;
                        break;
                    case Constants.Enum.WM_RBUTTONUP:
                        mouseUp = true;
                        button = MouseButton.Right;
                        clickCount = 1;
                        break;
                    case Constants.Enum.WM_RBUTTONDBLCLK:
                        button = MouseButton.Right;
                        clickCount = 2;
                        break;
                    case Constants.Enum.WM_MOUSEWHEEL:
                        //If the message is WM_MOUSEWHEEL, the high-order word of MouseData member is the wheel delta. 
                        //One wheel click is defined as WHEEL_DELTA, which is 120. 
                        //(value >> 16) & 0xffff; retrieves the high-order word from the given 32-bit value
                        mouseDelta = (short)((mouseHookStruct.MouseData >> 16) & 0xffff);

                        //TODO: X BUTTONS (I havent them so was unable to test)
                        //If the message is WM_XBUTTONDOWN, WM_XBUTTONUP, WM_XBUTTONDBLCLK, WM_NCXBUTTONDOWN, WM_NCXBUTTONUP, 
                        //or WM_NCXBUTTONDBLCLK, the high-order word specifies which X button was pressed or released, 
                        //and the low-order word is reserved. This value can be one or more of the following values. 
                        //Otherwise, MouseData is not used. 
                        break;
                }
                if (!button.HasValue)
                    return DLLImport.CallNextHookEx(hookId, nCode, wParam, lParam);
                RawMouseEventArgs e = new RawMouseEventArgs(mouseHookStruct.Point, button.Value, clickCount, mouseDelta);

                //Mouse up
                if (mouseUp)
                {
                    MouseDown.Invoke(null, e);
                }

                //Mouse down
                if (mouseDown)
                {
                    MouseUp.Invoke(null, e);
                }

                //If someone listens to click and a click is heppened
                if (clickCount > 0)
                {
                    MouseClick.Invoke(null, e);
                }

                //If someone listens to double click and a click is heppened
                if (clickCount == 2)
                {
                    MouseDoubleClick.Invoke(null, e);
                }

                //Wheel was moved
                if (mouseDelta != 0)
                {
                    MouseWheel.Invoke(null, e);
                }

                //If someone listens to move and there was a change in coordinates raise move event
                if (m_OldX != mouseHookStruct.Point.X || m_OldY != mouseHookStruct.Point.Y)
                {
                    m_OldX = (int)mouseHookStruct.Point.X;
                    m_OldY = (int)mouseHookStruct.Point.Y;

                    MouseMove.Invoke(null, e);
                }
            }
            if (KeyIgnore)
                return (IntPtr)1;
            return DLLImport.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseLLHookStruct
        {
            public Point Point;
            public int MouseData;
            public int Flags;
            public int Time;
            public int ExtraInfo;
        }

        //    private static IntPtr HookCallbackInner(  ///////////////////////////////////////백업
        //int nCode, IntPtr wParam, IntPtr lParam)
        //    {
        //        if (nCode >= 0 && Constants.Enum.WM_LBUTTONDOWN == (Constants.Enum)wParam)
        //        {
        //            MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

        //            Console.WriteLine(hookStruct.pt.x + ", " + hookStruct.pt.y);
        //        }
        //        if (KeyIgnore)
        //            return (IntPtr)1;
        //        return DLLImport.CallNextHookEx(hookId, nCode, wParam, lParam);
        //    }

    }

    #region 기능 함수
    public static class HookFunction
    {
        public static void SetFocus(IntPtr handle)
        {
            DLLImport.SetForegroundWindow(handle);
        }

        public static void SendLeftButtonDown(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_LBUTTONDOWN, 0, new IntPtr(y * 0x10000 + x));

        public static void SendLeftButtonUp(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_LBUTTONUP, 0, new IntPtr(y * 0x10000 + x));

        public static void SendLeftButtondblclick(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_LBUTTONDBLCLK, 0, new IntPtr(y * 0x10000 + x));

        public static void SendRightButtonDown(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_RBUTTONDOWN, 0, new IntPtr(y * 0x10000 + x));

        public static void SendRightButtonUp(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_RBUTTONUP, 0, new IntPtr(y * 0x10000 + x));

        public static void SendRightButtondblclick(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_RBUTTONDBLCLK, 0, new IntPtr(y * 0x10000 + x));

        public static void SendMouseMove(IntPtr handle, int x, int y) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_MOUSEMOVE, 0, new IntPtr(y * 0x10000 + x));

        public static void SendKeyDown(IntPtr handle, int key) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_KEYDOWN, key, IntPtr.Zero);

        public static void SendKeyUp(IntPtr handle, int key) =>
            DLLImport.PostMessage(handle, (int)Constants.Enum.WM_KEYUP, key, new IntPtr(1));

        public static void SendChar(IntPtr handle, char c) =>
            DLLImport.SendMessage(handle, (int)Constants.Enum.WM_CHAR, c, IntPtr.Zero);

        public static void SendString(IntPtr handle, string s)
        { foreach (char c in s) SendChar(handle, c); }
        /*
        *  0x0000 이전에 누른 적이 없고 호출 시점에도 눌려있지 않은 상태
         *  0x0001 이전에 누른 적이 있고 호출 시점에는 눌려있지 않은 상태
         *  0x8000 이전에 누른 적이 없고 호출 시점에는 눌려있는 상태
         *  0x8001 이전에 누른 적이 있고 호출 시점에도 눌려있는 상태
         */

        //enum RETURN_GetAsyncKeyState : ushort
        //{
        //    NN = 0x0000, // 0
        //    YN = 0x0001, // 1
        //    NY = 0x8000, // 32768
        //    YY = 0x8001  // 32769
        //}

        //public static bool IsKeyPress(VKCode key)
        //{
        //    RETURN_GetAsyncKeyState rtnVal = (RETURN_GetAsyncKeyState)InterceptKeys.GetAsyncKeyState((int)key);

        //    return RETURN_GetAsyncKeyState.YN.Equals(rtnVal) || RETURN_GetAsyncKeyState.YY.Equals(rtnVal);
        //}
    }
    #endregion
}