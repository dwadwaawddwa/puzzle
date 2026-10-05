using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>Finds every class tagged [PuzzleMode] (any loaded assembly) and creates modes by id.</summary>
    public static class PuzzleModeRegistry
    {
        public sealed class Entry
        {
            public string Id;
            public PuzzleModeAttribute Info;
            public Func<IPuzzleMode> Factory;
        }

        static Dictionary<string, Entry> _entries;

        static Dictionary<string, Entry> Entries
        {
            get
            {
                if (_entries == null) Scan();
                return _entries;
            }
        }

        public static IEnumerable<string> Ids => Entries.Keys;

        public static bool IsRegistered(string id) => id != null && Entries.ContainsKey(id);

        public static PuzzleModeAttribute InfoOf(string id) =>
            id != null && Entries.TryGetValue(id, out var e) ? e.Info : null;

        public static IPuzzleMode Create(string id)
        {
            if (id == null || !Entries.TryGetValue(id, out var e))
                throw new ArgumentException($"Unknown puzzle mode \"{id}\"");
            return e.Factory();
        }

        /// <summary>Manual registration (e.g. a mode living in a plugin assembly loaded later).</summary>
        public static void Register(PuzzleModeAttribute info, Func<IPuzzleMode> factory)
        {
            Entries[info.Id] = new Entry { Id = info.Id, Info = info, Factory = factory };
        }

        static void Scan()
        {
            _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(IPuzzleMode).IsAssignableFrom(t)) continue;
                    var attr = (PuzzleModeAttribute)Attribute.GetCustomAttribute(t, typeof(PuzzleModeAttribute));
                    if (attr == null) continue;
                    var type = t;
                    _entries[attr.Id] = new Entry
                    {
                        Id = attr.Id,
                        Info = attr,
                        Factory = () => (IPuzzleMode)Activator.CreateInstance(type)
                    };
                }
            }
        }
    }
}
