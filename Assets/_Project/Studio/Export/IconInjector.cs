using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PuzzleStudio.Studio.Export
{
    /// <summary>
    /// Replaces the icon embedded in a Windows .exe (Explorer, taskbar, window icon) using the Win32
    /// resource update API — no external tool such as rcedit needed.
    /// </summary>
    public static class IconInjector
    {
        public struct ResName
        {
            public ushort Id;      // when Name is null
            public string Name;
            public ushort Lang;
            public override string ToString() => Name ?? $"#{Id}";
        }

        /// <summary>Lists icon groups (RT_GROUP_ICON) and single icons (RT_ICON) of an executable.</summary>
        public static (List<ResName> groups, List<ResName> icons) ListIcons(string exePath)
        {
            var groups = new List<ResName>();
            var icons = new List<ResName>();
            IntPtr module = LoadLibraryExW(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot read resources of {exePath}");
            try
            {
                Collect(module, RT_GROUP_ICON, groups);
                Collect(module, RT_ICON, icons);
            }
            finally { FreeLibrary(module); }
            return (groups, icons);
        }

        public static void Inject(string exePath, List<IconBuilder.IconImage> images)
        {
            var (groups, icons) = ListIcons(exePath);
            ResName group = groups.Count > 0 ? groups[0] : new ResName { Id = 1, Lang = 1033 };
            ushort lang = group.Lang;

            IntPtr h = BeginUpdateResourceW(exePath, false);
            if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "BeginUpdateResource failed");
            bool ok = false;
            try
            {
                foreach (var icon in icons) Update(h, RT_ICON, icon, null);
                foreach (var g in groups) Update(h, RT_GROUP_ICON, g, null);

                // New icon images get ids that cannot clash with the ones just deleted.
                ushort firstId = 1;
                foreach (var icon in icons) if (icon.Name == null && icon.Id >= firstId) firstId = (ushort)(icon.Id + 1);
                for (int i = 0; i < images.Count; i++)
                    Update(h, RT_ICON, new ResName { Id = (ushort)(firstId + i), Lang = lang }, images[i].Data);

                // Keep the original group name so the player still finds its icon.
                Update(h, RT_GROUP_ICON, new ResName { Id = group.Id, Name = group.Name, Lang = lang },
                    IconBuilder.BuildGroupResource(images, firstId));
                ok = true;
            }
            finally
            {
                if (!EndUpdateResourceW(h, !ok) && ok)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "EndUpdateResource failed");
            }
        }

        static void Update(IntPtr h, IntPtr type, ResName name, byte[] data)
        {
            bool ok;
            if (name.Name != null)
            {
                IntPtr str = Marshal.StringToHGlobalUni(name.Name);
                try { ok = UpdateResourceW(h, type, str, name.Lang, data, (uint)(data?.Length ?? 0)); }
                finally { Marshal.FreeHGlobal(str); }
            }
            else ok = UpdateResourceW(h, type, (IntPtr)name.Id, name.Lang, data, (uint)(data?.Length ?? 0));
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), $"UpdateResource failed for {name}");
        }

        static List<ResName> _collecting;
        static IntPtr _collectType;

        static void Collect(IntPtr module, IntPtr type, List<ResName> into)
        {
            _collecting = into;
            _collectType = type;
            EnumResourceNamesW(module, type, OnName, IntPtr.Zero);
            _collecting = null;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumResNameProc))]
        static bool OnName(IntPtr module, IntPtr type, IntPtr name, IntPtr param)
        {
            var baseName = IsIntResource(name)
                ? new ResName { Id = (ushort)name.ToInt64() }
                : new ResName { Name = Marshal.PtrToStringUni(name) };
            _langs = new List<ushort>();
            EnumResourceLanguagesW(module, type, name, OnLang, IntPtr.Zero);
            if (_langs.Count == 0) _langs.Add(1033);
            foreach (var lang in _langs)
                _collecting.Add(new ResName { Id = baseName.Id, Name = baseName.Name, Lang = lang });
            return true;
        }

        static List<ushort> _langs;

        [AOT.MonoPInvokeCallback(typeof(EnumResLangProc))]
        static bool OnLang(IntPtr module, IntPtr type, IntPtr name, ushort lang, IntPtr param)
        {
            _langs.Add(lang);
            return true;
        }

        static bool IsIntResource(IntPtr p) => ((ulong)p.ToInt64() >> 16) == 0;

        // ------------------------------------------------------------------ interop

        static readonly IntPtr RT_ICON = (IntPtr)3;
        static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
        const uint LOAD_LIBRARY_AS_DATAFILE = 0x2;
        const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x20;

        delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
        delegate bool EnumResLangProc(IntPtr module, IntPtr type, IntPtr name, ushort lang, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr BeginUpdateResourceW(string file, bool deleteExisting);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateResourceW(IntPtr h, IntPtr type, IntPtr name, ushort lang, byte[] data, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool EndUpdateResourceW(IntPtr h, bool discard);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryExW(string file, IntPtr hFile, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr h);
        [DllImport("kernel32.dll")]
        static extern bool EnumResourceNamesW(IntPtr module, IntPtr type, EnumResNameProc cb, IntPtr param);
        [DllImport("kernel32.dll")]
        static extern bool EnumResourceLanguagesW(IntPtr module, IntPtr type, IntPtr name, EnumResLangProc cb, IntPtr param);
    }
}
