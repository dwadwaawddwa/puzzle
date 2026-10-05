using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PuzzleStudio.Core.Util.Win32
{
    /// <summary>
    /// Native Windows file/folder dialogs through plain P/Invoke (comdlg32 / shell32), no external DLL.
    /// They are modal: Unity pauses while the dialog is open.
    /// </summary>
    public static class FileDialogs
    {
        public const string ImageFilter = "Images (*.png, *.jpg)\0*.png;*.jpg;*.jpeg\0All files (*.*)\0*.*\0\0";
        public const string PngFilter = "PNG image (*.png)\0*.png\0Images (*.png, *.jpg)\0*.png;*.jpg;*.jpeg\0\0";
        public const string AudioFilter = "Sounds (*.ogg, *.wav, *.mp3)\0*.ogg;*.wav;*.mp3\0All files (*.*)\0*.*\0\0";
        public const string FontFilter = "Fonts (*.ttf, *.otf)\0*.ttf;*.otf\0All files (*.*)\0*.*\0\0";
        public const string ProjectFilter = "Puzzle Studio project (project.json)\0project.json\0\0";

        /// <returns>Selected files (empty if cancelled).</returns>
        public static List<string> OpenFiles(string title, string filter, bool multiSelect, string initialDir = null)
        {
            var result = new List<string>();
            const int bufferChars = 65536;
            IntPtr buffer = Marshal.AllocHGlobal(bufferChars * 2);
            try
            {
                // Zero the buffer (no initial file name).
                Marshal.Copy(new byte[bufferChars * 2], 0, buffer, bufferChars * 2);
                var ofn = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                    hwndOwner = NativeWindow.FindMainWindow(),
                    lpstrFilter = filter,
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = bufferChars,
                    lpstrInitialDir = initialDir,
                    lpstrTitle = title,
                    Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | (multiSelect ? OFN_ALLOWMULTISELECT : 0),
                };
                if (!GetOpenFileNameW(ref ofn)) return result;

                // Single: "C:\dir\file.png\0". Multi: "C:\dir\0a.png\0b.png\0\0".
                var parts = new List<string>();
                int offset = 0;
                while (true)
                {
                    string s = Marshal.PtrToStringUni(IntPtr.Add(buffer, offset * 2));
                    if (string.IsNullOrEmpty(s)) break;
                    parts.Add(s);
                    offset += s.Length + 1;
                }
                if (parts.Count == 1) result.Add(parts[0]);
                else if (parts.Count > 1)
                    for (int i = 1; i < parts.Count; i++) result.Add(Path.Combine(parts[0], parts[i]));
            }
            finally { Marshal.FreeHGlobal(buffer); }
            return result;
        }

        public static string OpenFile(string title, string filter, string initialDir = null)
        {
            var files = OpenFiles(title, filter, false, initialDir);
            return files.Count > 0 ? files[0] : null;
        }

        /// <returns>The chosen folder, or null if cancelled.</returns>
        public static string PickFolder(string title)
        {
            CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED); // needed by BIF_NEWDIALOGSTYLE; harmless if already done
            var bi = new BROWSEINFO
            {
                hwndOwner = NativeWindow.FindMainWindow(),
                lpszTitle = title,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE | BIF_EDITBOX,
            };
            IntPtr pidl = SHBrowseForFolderW(ref bi);
            if (pidl == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                return SHGetPathFromIDListW(pidl, sb) ? sb.ToString() : null;
            }
            finally { CoTaskMemFree(pidl); }
        }

        /// <summary>Opens Explorer on a folder, or with a file selected.</summary>
        public static void Reveal(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                else if (Directory.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ interop

        const int OFN_ALLOWMULTISELECT = 0x200;
        const int OFN_PATHMUSTEXIST = 0x800;
        const int OFN_FILEMUSTEXIST = 0x1000;
        const int OFN_NOCHANGEDIR = 0x8;
        const int OFN_EXPLORER = 0x80000;
        const uint BIF_RETURNONLYFSDIRS = 0x1;
        const uint BIF_EDITBOX = 0x10;
        const uint BIF_NEWDIALOGSTYLE = 0x40;
        const uint COINIT_APARTMENTTHREADED = 0x2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHBrowseForFolderW(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);

        [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr p);
        [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint coInit);
    }
}
