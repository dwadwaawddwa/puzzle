using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PuzzleStudio.Core.Util.Win32
{
    /// <summary>
    /// Receives files/folders dropped from Windows Explorer onto the player window
    /// (Unity has no built-in support for this in builds). Uses a WH_GETMESSAGE hook on the main thread.
    /// Call <see cref="Install"/> once, then <see cref="TryDequeue"/> from Update.
    /// </summary>
    public static class DragDropHook
    {
        public static bool IsInstalled => _hook != IntPtr.Zero;

        static IntPtr _hook;
        static HookProc _proc;   // keep the delegate alive
        static readonly Queue<List<string>> Pending = new Queue<List<string>>();

        public static bool Install()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hook != IntPtr.Zero) return true;
            IntPtr hwnd = NativeWindow.FindMainWindow();
            if (hwnd == IntPtr.Zero) return false;
            DragAcceptFiles(hwnd, true);
            _proc = Callback;
            _hook = SetWindowsHookExW(WH_GETMESSAGE, _proc, IntPtr.Zero, GetCurrentThreadId());
            return _hook != IntPtr.Zero;
#else
            return false;
#endif
        }

        public static void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        public static bool TryDequeue(out List<string> paths)
        {
            lock (Pending)
            {
                if (Pending.Count == 0) { paths = null; return false; }
                paths = Pending.Dequeue();
                return true;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(HookProc))]
        static IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && lParam != IntPtr.Zero)
            {
                var msg = Marshal.PtrToStructure<MSG>(lParam);
                if (msg.message == WM_DROPFILES)
                {
                    IntPtr hDrop = msg.wParam;
                    uint count = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
                    var list = new List<string>((int)count);
                    for (uint i = 0; i < count; i++)
                    {
                        uint len = DragQueryFileW(hDrop, i, null, 0);
                        var sb = new StringBuilder((int)len + 1);
                        DragQueryFileW(hDrop, i, sb, len + 1);
                        list.Add(sb.ToString());
                    }
                    DragFinish(hDrop);
                    lock (Pending) Pending.Enqueue(list);
                }
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        // ------------------------------------------------------------------ interop

        const int WH_GETMESSAGE = 3;
        const uint WM_DROPFILES = 0x0233;

        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookExW(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("shell32.dll")] static extern void DragAcceptFiles(IntPtr hwnd, bool accept);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder file, uint cch);
        [DllImport("shell32.dll")] static extern void DragFinish(IntPtr hDrop);
    }
}
