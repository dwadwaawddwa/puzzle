using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PuzzleStudio.Core.Util.Win32
{
    /// <summary>
    /// Renames the player window at runtime. The Player Template is compiled once with a generic product name,
    /// so each exported game sets its real title here.
    /// </summary>
    public static class WindowTitle
    {
        public static bool Set(string title)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr hwnd = NativeWindow.FindMainWindow();
            return hwnd != IntPtr.Zero && SetWindowTextW(hwnd, title);
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetWindowTextW(IntPtr hWnd, string text);
#endif
    }

    /// <summary>Finds this process' Unity window (owner for native dialogs, title changes).</summary>
    public static class NativeWindow
    {
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr hWnd, StringBuilder sb, int max);

        static IntPtr _found;
        static uint _pid;
        static IntPtr _cached;

        /// <summary>The player window, or IntPtr.Zero (always zero in the editor).</summary>
        public static IntPtr FindMainWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_cached != IntPtr.Zero) return _cached;
            _found = IntPtr.Zero;
            _pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows(Callback, IntPtr.Zero);
            _cached = _found;
            return _found;
#else
            return IntPtr.Zero;
#endif
        }

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        static bool Callback(IntPtr hWnd, IntPtr lParam)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != _pid) return true;
            var sb = new StringBuilder(64);
            GetClassNameW(hWnd, sb, sb.Capacity);
            if (sb.ToString() != "UnityWndClass") return true;
            _found = hWnd;
            return false;
        }
    }
}
