using System.IO;
using UnityEngine;

namespace PuzzleStudio.Studio.App
{
    public static class StudioPaths
    {
        public const string TemplateExe = "Game.exe";
        public const string TemplateData = "Game_Data";

        /// <summary>
        /// Folder holding the compiled Player Template. Built Studio: &lt;Studio&gt;/Template.
        /// Editor: &lt;project&gt;/Build/Template (made by Build &gt; Player Template).
        /// </summary>
        public static string TemplateDir
        {
            get
            {
                string appRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#if UNITY_EDITOR
                return Path.Combine(appRoot, "Build", "Template");
#else
                return Path.Combine(appRoot, "Template");
#endif
            }
        }

        /// <summary>Sample Game Packs offered on the welcome screen.</summary>
        public static string SamplesDir
        {
            get
            {
                string appRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#if UNITY_EDITOR
                return Path.Combine(appRoot, "SamplePacks");
#else
                return Path.Combine(appRoot, "Samples");
#endif
            }
        }

        public static string TemplateExePath => Path.Combine(TemplateDir, TemplateExe);

        public static bool TemplateAvailable =>
            File.Exists(TemplateExePath) && Directory.Exists(Path.Combine(TemplateDir, TemplateData));
    }
}
