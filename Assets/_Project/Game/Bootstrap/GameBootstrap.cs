using System;
using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Localization;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Game.UI;
using UnityEngine;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>Finds and loads the Game Pack, then initialises every service.</summary>
    public static class GameBootstrap
    {
        public static bool IsInitialized { get; private set; }

        /// <summary>Editor-only fallback pack (project root relative) when nothing else is found.</summary>
        public const string EditorSamplePack = "SamplePacks/CozyPastel";

        public static void EnsureInitialized()
        {
            if (!IsInitialized) Initialize(LocatePack());
        }

        /// <summary>
        /// Pack lookup order: command line "-pack &lt;dir&gt;", StreamingAssets/GamePack, then (editor only) the sample pack.
        /// </summary>
        public static string LocatePack()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], PackPaths.PackArg, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(args[i + 1]);

            string streaming = Path.Combine(Application.streamingAssetsPath, PackPaths.StreamingFolder);
            if (File.Exists(Path.Combine(streaming, PackPaths.GameJson))) return streaming;

#if UNITY_EDITOR
            string sample = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EditorSamplePack));
            if (Directory.Exists(sample)) return sample;
#endif
            return streaming;
        }

        public static void Initialize(string packDir)
        {
            ServiceHub.Clear();
            GamePackData pack;
            try
            {
                pack = GamePackLoader.LoadFromDirectory(packDir);
                Debug.Log($"[Puzzle] Loaded pack \"{pack.game.title}\" ({pack.levels.Count} levels) from {pack.RootPath}");
            }
            catch (PackLoadException e)
            {
                Debug.LogError($"[Puzzle] {e.Message}");
                InitializeWithPack(new GamePackData());
                ServiceHub.LoadError = e.Message;
                return;
            }
            InitializeWithPack(pack);
        }

        /// <summary>Also used by the Studio live preview with an in-memory pack (<paramref name="preview"/> = true:
        /// throw-away saves, window title untouched).</summary>
        public static void InitializeWithPack(GamePackData pack, bool preview = false)
        {
            ServiceHub.Clear();
            ServiceHub.Pack = pack;

            string saveRoot = preview ? Path.Combine(Application.temporaryCachePath, "StudioPreview") : Application.persistentDataPath;
            var save = new SaveSystem(saveRoot, pack.game.title);
            save.ProgressStore.OnWarning += Debug.LogWarning;
            save.SettingsStore.OnWarning += Debug.LogWarning;
            save.LoadAll();
            ServiceHub.Save = save;

            string lang = save.Settings.language ?? pack.game.defaultLanguage ?? "en";
            ServiceHub.Loc = LoadLocalization(pack, lang);
            ServiceHub.Theme = new ThemeService(pack.theme, pack);

            if (!preview) WindowTitle.Set(pack.game.title);
            IsInitialized = true;
        }

        public static LocalizationService LoadLocalization(GamePackData pack, string lang)
        {
            var loc = new LocalizationService();
            string builtinEn = Resources.Load<TextAsset>("Localization/en")?.text;
            string builtinLang = lang != "en" ? Resources.Load<TextAsset>($"Localization/{lang}")?.text : null;
            loc.Load(lang, builtinEn, builtinLang, ReadPackLocale(pack, "en"), lang != "en" ? ReadPackLocale(pack, lang) : null);
            return loc;
        }

        static string ReadPackLocale(GamePackData pack, string lang)
        {
            if (string.IsNullOrEmpty(pack.RootPath)) return null;
            string path = Path.Combine(pack.RootPath, PackPaths.LocaleDir, lang + ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public static void Shutdown()
        {
            ServiceHub.Clear();
            IsInitialized = false;
        }
    }
}
