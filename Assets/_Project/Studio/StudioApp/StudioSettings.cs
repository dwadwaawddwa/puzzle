using System;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Save;
using UnityEngine;

namespace PuzzleStudio.Studio.App
{
    /// <summary>Per-user Studio preferences (recent projects...), stored in persistentDataPath/studio.json.</summary>
    [Serializable]
    public sealed class StudioSettings
    {
        public const int MaxRecent = 10;

        public List<string> recentProjects = new List<string>();
        public string lastProject = null;

        static JsonFileStore<StudioSettings> _store;
        static JsonFileStore<StudioSettings> Store =>
            _store ??= new JsonFileStore<StudioSettings>(Path.Combine(Application.persistentDataPath, "studio.json"));

        public static StudioSettings Load() => Store.Load();
        public void Save() => Store.Save(this);

        public void AddRecent(string projectRoot)
        {
            recentProjects.RemoveAll(p => string.Equals(p, projectRoot, StringComparison.OrdinalIgnoreCase));
            recentProjects.Insert(0, projectRoot);
            if (recentProjects.Count > MaxRecent) recentProjects.RemoveRange(MaxRecent, recentProjects.Count - MaxRecent);
            lastProject = projectRoot;
        }

        public List<string> ExistingRecent()
        {
            recentProjects.RemoveAll(p => StudioProject.ResolveRoot(p) == null);
            return recentProjects;
        }
    }
}
