using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PuzzleStudio.Core.Audio
{
    /// <summary>
    /// Music with crossfade (two sources), pooled SFX with pitch variation, and clip loading from either the
    /// built-in library ("default:calm_01" → Resources/Audio/calm_01) or files of the Game Pack (ogg/wav/mp3).
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public const int SfxVoices = 10;

        GamePackData _pack;
        AudioSource _musicA, _musicB, _activeMusic;
        readonly List<AudioSource> _sfx = new List<AudioSource>();
        int _nextVoice;
        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, List<Action<AudioClip>>> _pending = new Dictionary<string, List<Action<AudioClip>>>();
        string _musicRef;
        float _fadeSpeed = 1f;

        public float Master { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 0.6f;
        public float SfxVolume { get; private set; } = 0.8f;
        /// <summary>Silences everything (Studio preview by default).</summary>
        public bool Muted { get; set; }

        public void Init(GamePackData pack, float master, float music, float sfx)
        {
            _pack = pack;
            if (_musicA == null)
            {
                _musicA = CreateSource("Music A", loop: true);
                _musicB = CreateSource("Music B", loop: true);
                for (int i = 0; i < SfxVoices; i++) _sfx.Add(CreateSource($"Sfx {i}", loop: false));
            }
            SetVolumes(master, music, sfx);
            float cross = pack?.audio.crossfadeSeconds ?? 1.5f;
            _fadeSpeed = cross > 0.01f ? 1f / cross : 100f;
        }

        public void SetVolumes(float master, float music, float sfx)
        {
            Master = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
        }

        // ------------------------------------------------------------------ music

        public void PlayMusic(string reference)
        {
            if (string.IsNullOrEmpty(reference) || reference == _musicRef) return;
            _musicRef = reference;
            Load(reference, clip =>
            {
                if (clip == null || reference != _musicRef) return;
                var next = _activeMusic == _musicA ? _musicB : _musicA;
                if (next.clip == clip && next.isPlaying) { _activeMusic = next; return; }
                next.clip = clip;
                next.volume = 0f;
                next.Play();
                _activeMusic = next;
            });
        }

        public void StopMusic()
        {
            _musicRef = null;
            _activeMusic = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float target = Muted ? 0f : Master * MusicVolume;
            Fade(_musicA, _activeMusic == _musicA ? target : 0f, dt);
            Fade(_musicB, _activeMusic == _musicB ? target : 0f, dt);
        }

        void Fade(AudioSource s, float target, float dt)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, dt * _fadeSpeed * Mathf.Max(0.05f, Master * MusicVolume));
            if (s.volume <= 0.0001f && target <= 0f && s.isPlaying) s.Stop();
        }

        // ------------------------------------------------------------------ sfx

        /// <param name="key">"click", "pick", "drop", "snap", "victory", "star", "locked"… (mapped in game.json audio.sfx).</param>
        public void PlaySfx(string key, float pitch = 1f, float volume = 1f)
        {
            if (Muted || Master * SfxVolume <= 0f) return;
            string reference = null;
            if (_pack != null && _pack.audio.sfx != null) _pack.audio.sfx.TryGetValue(key, out reference);
            if (string.IsNullOrEmpty(reference)) reference = "default:" + key;

            float variation = _pack?.audio.pitchVariation ?? 0.08f;
            float finalPitch = pitch * (1f + UnityEngine.Random.Range(-variation, variation));
            Load(reference, clip =>
            {
                if (clip == null) return;
                var src = _sfx[_nextVoice];
                _nextVoice = (_nextVoice + 1) % _sfx.Count;
                src.pitch = finalPitch;
                src.PlayOneShot(clip, Mathf.Clamp01(volume * Master * SfxVolume));
            });
        }

        // ------------------------------------------------------------------ loading

        public void Load(string reference, Action<AudioClip> onReady)
        {
            if (_cache.TryGetValue(reference, out var cached)) { onReady(cached); return; }

            if (PackPathsUtil.IsDefaultRef(reference))
            {
                var clip = Resources.Load<AudioClip>("Audio/" + PackPathsUtil.DefaultName(reference));
                if (clip == null) Debug.LogWarning($"[Audio] Built-in sound not found: {reference}");
                _cache[reference] = clip;
                onReady(clip);
                return;
            }

            string path = _pack?.Resolve(reference);
            if (path == null || !File.Exists(path))
            {
                Debug.LogWarning($"[Audio] Sound file not found: {reference}");
                _cache[reference] = null;
                onReady(null);
                return;
            }

            if (_pending.TryGetValue(reference, out var waiting)) { waiting.Add(onReady); return; }
            _pending[reference] = new List<Action<AudioClip>> { onReady };
            StartCoroutine(LoadFile(reference, path));
        }

        IEnumerator LoadFile(string reference, string path)
        {
            var type = AudioTypeFor(path);
            using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                yield return req.SendWebRequest();
                AudioClip clip = null;
                if (req.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(req);
                else Debug.LogWarning($"[Audio] Could not load {reference}: {req.error}");
                if (clip != null) clip.name = Path.GetFileNameWithoutExtension(path);
                _cache[reference] = clip;
                var callbacks = _pending[reference];
                _pending.Remove(reference);
                foreach (var cb in callbacks) cb(clip);
            }
        }

        public static AudioType AudioTypeFor(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                case ".mp3": return AudioType.MPEG;
                default: return AudioType.UNKNOWN;
            }
        }

        AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = loop ? 0f : 1f;
            return s;
        }
    }
}
