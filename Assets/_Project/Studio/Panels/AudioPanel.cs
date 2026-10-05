using System;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Widgets;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    /// <summary>Audio tab: menu / level music (built-in or imported), volumes, and every sound effect (listen, replace, reset).</summary>
    public sealed class AudioPanel : StudioPanel
    {
        static readonly (string reference, string label)[] BuiltInMusic =
        {
            ("default:calm_01", "Calm 1"), ("default:calm_02", "Calm 2"), ("default:calm_03", "Calm 3"), ("", "No music"),
        };

        static readonly (string key, string label)[] Sounds =
        {
            ("click", "Button click"), ("pick", "Pick a piece"), ("drop", "Drop a piece"), ("snap", "Piece in place"),
            ("star", "Star"), ("victory", "Victory"), ("hint", "Hint"), ("undo", "Undo"),
            ("locked", "Not allowed"), ("swoosh", "Swoosh"),
        };

        string _playing;

        public AudioPanel(StudioApp app) : base(app) { }
        public override string Id => "audio";
        public override string Title => "Audio";

        public override void OnDeactivated() => StopMusic();

        public override void Build(VisualElement content)
        {
            var a = Pack.audio;
            App.Audio.Init(Pack, 1f, a.musicVolume, a.sfxVolume);

            content.Add(Fields.Section("Music",
                MusicRow("Menus", a.musicMenu, v => a.musicMenu = v, "music_menu"),
                MusicRow("Levels", a.musicGame, v => a.musicGame = v, "music_game"),
                Fields.FloatSlider("Volume", 0f, 1f, a.musicVolume, v => { a.musicVolume = v; ApplyVolumes(); Saved(); }),
                Fields.FloatSlider("Crossfade (s)", 0f, 4f, a.crossfadeSeconds, v => { a.crossfadeSeconds = v; Saved(); }),
                Fields.Hint("OGG, WAV or MP3, looped. Only use music you may sell with your game (your own, or royalty-free with a license that allows games). The players can still change the volume in the settings.")));

            var sfx = Fields.Section("Sound effects",
                Fields.FloatSlider("Volume", 0f, 1f, a.sfxVolume, v => { a.sfxVolume = v; ApplyVolumes(); Saved(); }),
                Fields.FloatSlider("Pitch variation", 0f, 0.3f, a.pitchVariation, v => { a.pitchVariation = v; Saved(); }),
                Fields.Hint("A little pitch variation makes repeated sounds less tiring."));
            foreach (var (key, label) in Sounds) sfx.Add(SoundRow(key, label));
            content.Add(sfx);
        }

        // ------------------------------------------------------------------ music

        VisualElement MusicRow(string label, string current, Action<string> set, string baseName)
        {
            var choices = new List<string>();
            var refs = new List<string>();
            foreach (var (reference, name) in BuiltInMusic) { choices.Add(name); refs.Add(reference); }
            current ??= "";
            if (refs.IndexOf(current) < 0)
            {
                choices.Add("My file: " + Path.GetFileName(current));
                refs.Add(current);
            }
            var dropdown = Fields.Dropdown(label, choices, Math.Max(0, refs.IndexOf(current)), i =>
            {
                if (i < 0) return;
                StopMusic();
                set(refs[i]);
                Changed(rebuildInspector: true);
            });

            bool playing = !string.IsNullOrEmpty(current) && _playing == current;
            var play = Fields.Button(playing ? "Stop" : "Play", () =>
            {
                if (_playing == current) StopMusic();
                else if (!string.IsNullOrEmpty(current))
                {
                    App.Audio.StopMusic();
                    App.Audio.PlayMusic(current);
                    _playing = current;
                }
                App.RebuildInspector();
            }, "studio-btn--small");
            play.SetEnabled(!string.IsNullOrEmpty(current));

            var import = Fields.Button("Import…", () =>
            {
                string file = FileDialogs.OpenFile($"Choose the {label.ToLowerInvariant()} music", FileDialogs.AudioFilter);
                if (string.IsNullOrEmpty(file)) return;
                StopMusic();
                set(ProjectFiles.ImportAudioFile(Pack, file, baseName));
                Changed(rebuildInspector: true);
            }, "studio-btn--small");

            var row = Fields.Row(dropdown, play, import);
            row.AddToClassList("studio-audio-row");
            return row;
        }

        void StopMusic()
        {
            _playing = null;
            App.Audio?.StopMusic();
        }

        // ------------------------------------------------------------------ sound effects

        VisualElement SoundRow(string key, string label)
        {
            var sfx = Pack.audio.sfx;
            sfx.TryGetValue(key, out string current);
            bool custom = !string.IsNullOrEmpty(current) && !current.StartsWith("default:", StringComparison.OrdinalIgnoreCase);

            var name = new Label(label);
            name.AddToClassList("studio-sound-name");
            var file = Fields.Hint(custom ? Path.GetFileName(current) : "Built-in");
            file.AddToClassList("studio-sound-file");
            var play = Fields.Button("Play", () => App.Audio.PlaySfx(key), "studio-btn--small");
            var replace = Fields.Button("Replace…", () =>
            {
                string picked = FileDialogs.OpenFile($"Choose the \"{label}\" sound", FileDialogs.AudioFilter);
                if (string.IsNullOrEmpty(picked)) return;
                sfx[key] = ProjectFiles.ImportAudioFile(Pack, picked, "sfx_" + key);
                Changed(rebuildInspector: true);
            }, "studio-btn--small");
            var row = Fields.Row(name, file, Fields.Spacer(), play, replace,
                custom ? Fields.Button("Built-in", () => { sfx[key] = "default:" + key; Changed(rebuildInspector: true); }, "studio-btn--small") : null);
            row.AddToClassList("studio-sound-row");
            return row;
        }

        void ApplyVolumes() => App.Audio.SetVolumes(1f, Pack.audio.musicVolume, Pack.audio.sfxVolume);

        /// <summary>Audio settings don't change the picture: no preview rebuild.</summary>
        void Saved() => App.MarkDirty(false, refreshPreview: false);
    }
}
