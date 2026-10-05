using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    /// <summary>
    /// Texts tab: the credits, and every game text in every language. Changes are stored in game.json ("texts"):
    /// a built-in text left empty keeps its default; a new language starts from English until it is translated.
    /// </summary>
    public sealed class TextsPanel : StudioPanel
    {
        public static readonly (string code, string name)[] KnownLanguages =
        {
            ("en", "English"), ("fr", "French"), ("de", "German"), ("es", "Spanish"), ("it", "Italian"),
            ("pt", "Portuguese"), ("nl", "Dutch"), ("pl", "Polish"), ("tr", "Turkish"), ("ru", "Russian"),
            ("ja", "Japanese"), ("zh", "Chinese"), ("ko", "Korean"),
        };
        const int PageSize = 40;

        static readonly Dictionary<string, Dictionary<string, string>> BuiltIn = new Dictionary<string, Dictionary<string, string>>();

        string _lang = "en";
        string _filter = "";
        int _shown = PageSize;
        VisualElement _list;

        public TextsPanel(StudioApp app) : base(app) { }
        public override string Id => "texts";
        public override string Title => "Texts";

        public override void Build(VisualElement content)
        {
            BuildCredits(content);
            BuildLanguages(content);
        }

        // ------------------------------------------------------------------ credits

        void BuildCredits(VisualElement content)
        {
            var credits = Pack.game.credits;
            var section = Fields.Section("Credits");
            for (int i = 0; i < credits.Count; i++)
            {
                int index = i;
                var c = credits[i];
                var card = new VisualElement();
                card.AddToClassList("studio-ach");
                card.Add(Fields.Text("Role", c.role, v => { c.role = v; Typed(); }));
                card.Add(Fields.Text("Names", string.Join("\n", c.names), v =>
                {
                    c.names = v.Split('\n').Select(n => n.TrimEnd('\r')).ToList();
                    Typed();
                }, multiline: true));
                var up = Fields.Button("Up", () => { (credits[index - 1], credits[index]) = (credits[index], credits[index - 1]); Changed(rebuildInspector: true); }, "studio-btn--small");
                up.SetEnabled(index > 0);
                var down = Fields.Button("Down", () => { (credits[index + 1], credits[index]) = (credits[index], credits[index + 1]); Changed(rebuildInspector: true); }, "studio-btn--small");
                down.SetEnabled(index < credits.Count - 1);
                card.Add(Fields.Row(up, down, Fields.Spacer(),
                    Fields.Button("Remove", () => { credits.RemoveAt(index); Changed(rebuildInspector: true); }, "studio-btn--small studio-btn--danger")));
                section.Add(card);
            }
            section.Add(Fields.Row(Fields.Button("+ Add a credit", () =>
            {
                credits.Add(new CreditEntry { role = "Pictures", names = new List<string> { "Your name" } });
                Changed(rebuildInspector: true);
            }, "studio-btn--small")));
            section.Add(Fields.Hint("Shown on the Credits screen after the developer. One name per line. Fonts, audio and engine credits are added automatically."));
            content.Add(section);
        }

        // ------------------------------------------------------------------ languages

        void BuildLanguages(VisualElement content)
        {
            var languages = GameLanguages();
            if (!languages.Contains(_lang)) _lang = "en";

            var section = Fields.Section("Languages");
            var names = languages.Select(LanguageName).ToList();
            section.Add(Fields.Dropdown("Edit the texts in", names, languages.IndexOf(_lang), i =>
            {
                if (i < 0) return;
                _lang = languages[i];
                _shown = PageSize;
                App.RebuildInspector();
            }));

            var missing = KnownLanguages.Where(l => !languages.Contains(l.code)).ToList();
            if (missing.Count > 0)
            {
                var picker = Fields.Dropdown("Add a language", missing.Select(l => l.name).ToList(), 0, _ => { });
                section.Add(Fields.Row(picker, Fields.Button("Add", () =>
                {
                    int i = missing.FindIndex(l => l.name == picker.value);
                    if (i < 0) return;
                    _lang = missing[i].code;
                    if (!Pack.texts.ContainsKey(_lang)) Pack.texts[_lang] = new Dictionary<string, string>();
                    Changed(rebuildInspector: true);
                }, "studio-btn--small")));
            }

            bool builtIn = HasBuiltIn(_lang);
            var overrides = Overrides(_lang, create: false);
            int total = Defaults(_lang).Count;
            int changed = overrides?.Count(kv => !string.IsNullOrEmpty(kv.Value)) ?? 0;
            section.Add(Fields.Hint(builtIn
                ? $"{LanguageName(_lang)} is built in: {changed} text(s) changed by you. Empty fields keep the default text."
                : $"{changed} / {total} texts translated. Texts left empty are shown in English."));
            if (!builtIn)
                section.Add(Fields.Row(Fields.Button($"Remove {LanguageName(_lang)}", () =>
                    App.Modal.Confirm("Remove language", $"Remove {LanguageName(_lang)} and its {changed} translated text(s)?", "Remove", () =>
                    {
                        Pack.texts.Remove(_lang);
                        _lang = "en";
                        Changed(rebuildInspector: true);
                    }, "studio-btn--danger"), "studio-btn--small studio-btn--danger")));
            section.Add(Fields.Hint("Players choose the language in the game settings; new players get Steam's language when the game has it."));
            content.Add(section);

            var texts = Fields.Section($"Texts · {LanguageName(_lang)}");
            var search = Fields.Text("Search", _filter, v =>
            {
                _filter = v ?? "";
                _shown = PageSize;
                FillList();
            });
            search.textEdition.placeholder = "menu, victory, Play…";
            texts.Add(search);
            _list = new VisualElement();
            texts.Add(_list);
            content.Add(texts);
            FillList();
        }

        void FillList()
        {
            if (_list == null) return;
            _list.Clear();
            var defaults = Defaults(_lang);
            var english = Defaults("en");
            var overrides = Overrides(_lang, create: false);
            string f = _filter.Trim();
            var keys = defaults.Keys.Where(k => f.Length == 0
                    || k.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                    || defaults[k].IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                    || (overrides != null && overrides.TryGetValue(k, out var o) && o != null && o.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            foreach (var key in keys.Take(_shown))
            {
                var card = new VisualElement();
                card.AddToClassList("studio-text-row");
                var keyLabel = new Label(key);
                keyLabel.AddToClassList("studio-text-key");
                card.Add(keyLabel);
                string fallback = defaults[key];
                if (_lang != "en" && !HasBuiltIn(_lang) && english.TryGetValue(key, out var en)) fallback = en;
                string value = overrides != null && overrides.TryGetValue(key, out var v) ? v : "";
                var field = new TextField { value = value ?? "", multiline = fallback.Length > 60 };
                field.AddToClassList("studio-field");
                field.AddToClassList("studio-text-field");
                field.textEdition.placeholder = fallback;
                string k = key;
                field.RegisterValueChangedCallback(e => SetText(k, e.newValue));
                // The live preview follows when the field is left (rebuilding it on every key would be slow).
                field.RegisterCallback<FocusOutEvent>(_ => App.MarkDirty(false, refreshPreview: true));
                card.Add(field);
                _list.Add(card);
            }
            if (keys.Count > _shown)
                _list.Add(Fields.Row(
                    Fields.Hint($"Showing {_shown} of {keys.Count}."),
                    Fields.Button("Show more", () => { _shown += PageSize; FillList(); }, "studio-btn--small")));
            else if (keys.Count == 0)
                _list.Add(Fields.Hint("No text matches."));
            StudioFont.Apply(_list);
        }

        void SetText(string key, string value)
        {
            var overrides = Overrides(_lang, create: true);
            if (string.IsNullOrEmpty(value)) overrides.Remove(key);
            else overrides[key] = value;
            // A built-in language without any change does not need an entry.
            if (overrides.Count == 0 && (HasBuiltIn(_lang) || _lang == "en")) Pack.texts.Remove(_lang);
            Typed();
        }

        Dictionary<string, string> Overrides(string lang, bool create)
        {
            if (Pack.texts.TryGetValue(lang, out var d) && d != null) return d;
            if (!create) return null;
            d = new Dictionary<string, string>();
            Pack.texts[lang] = d;
            return d;
        }

        /// <summary>English, the built-in translations and every language the pack has texts or locale files for.</summary>
        List<string> GameLanguages()
        {
            var list = new List<string> { "en" };
            foreach (var (code, _) in KnownLanguages)
                if (code != "en" && HasBuiltIn(code)) list.Add(code);
            foreach (var code in Pack.texts.Keys)
                if (!list.Contains(code)) list.Add(code);
            if (!string.IsNullOrEmpty(Pack.RootPath))
            {
                string dir = Path.Combine(Pack.RootPath, PackPaths.LocaleDir);
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        if (!list.Contains(code)) list.Add(code);
                    }
            }
            return list;
        }

        public static string LanguageName(string code)
        {
            foreach (var (c, name) in KnownLanguages) if (c == code) return name;
            return code.ToUpperInvariant();
        }

        static bool HasBuiltIn(string code) => code == "en" || Resources.Load<TextAsset>("Localization/" + code) != null;

        /// <summary>Built-in texts of a language (English for languages the game does not ship).</summary>
        static Dictionary<string, string> Defaults(string code)
        {
            if (!HasBuiltIn(code)) code = "en";
            if (BuiltIn.TryGetValue(code, out var d)) return d;
            var asset = Resources.Load<TextAsset>("Localization/" + code);
            d = asset != null ? PackJson.Deserialize<Dictionary<string, string>>(asset.text) : new Dictionary<string, string>();
            BuiltIn[code] = d;
            return d;
        }

        /// <summary>Typing: record the change (undo, title) without rebuilding the inspector or the preview.</summary>
        void Typed() => App.MarkDirty(false, refreshPreview: false);
    }
}
