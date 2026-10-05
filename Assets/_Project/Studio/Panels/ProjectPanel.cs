using System.Collections.Generic;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Widgets;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    public sealed class ProjectPanel : StudioPanel
    {
        static readonly List<string> Languages = new List<string> { "en", "fr", "de", "es", "it", "pt", "ja", "zh" };

        public ProjectPanel(StudioApp app) : base(app) { }
        public override string Id => "project";
        public override string Title => "Project";

        public override void Build(VisualElement content)
        {
            var g = Pack.game;
            content.Add(Fields.Section("Game",
                Fields.Text("Title", g.title, v => { g.title = v; App.RefreshTitle(); Changed(); }),
                Fields.Text("Subtitle", g.subtitle, v => { g.subtitle = v; Changed(); }),
                Fields.Text("Developer", g.developer, v => { g.developer = v; Changed(); }),
                Fields.Text("Version", g.version, v => { g.version = v; Changed(); }),
                Fields.Hint("The title is shown in the window bar, the game screens and used as the default executable name.")));

            var appId = Fields.Text("Steam App ID", g.steamAppId.ToString(), v =>
            {
                if (long.TryParse(v, out long id) && id >= 0) { g.steamAppId = id; Changed(); }
            });
            content.Add(Fields.Section("Steam",
                appId,
                Fields.Hint("Leave 0 until Valve gives you an App ID (Steamworks → Create app). Achievements and Steam features are added in a later update.")));

            content.Add(Fields.Section("Language",
                Fields.Dropdown("Default language", Languages, Languages.IndexOf(g.defaultLanguage), i =>
                {
                    if (i >= 0) { g.defaultLanguage = Languages[i]; Changed(); }
                }),
                Fields.Hint("Built-in game texts are in English. Translations will be editable in the Texts tab (coming soon).")));

            content.Add(Fields.Section("Project folder",
                Fields.PathRow("", App.Project.Root, "Open", () => FileDialogs.Reveal(App.Project.Root)),
                Fields.Hint("Your images are copied into this folder; you can move the whole folder anywhere.")));
        }
    }
}
