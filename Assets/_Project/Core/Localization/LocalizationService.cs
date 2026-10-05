using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PuzzleStudio.Core.Localization
{
    /// <summary>
    /// Key → text table. Layers (later wins): built-in English, built-in language, pack English, pack language.
    /// Missing keys return the key itself so they are easy to spot.
    /// </summary>
    public sealed class LocalizationService
    {
        readonly Dictionary<string, string> _table = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Language { get; private set; } = "en";
        public event Action OnLanguageChanged;

        public int Count => _table.Count;

        /// <param name="layers">JSON objects { "key": "text" }, applied in order; null entries are skipped.</param>
        public void Load(string language, params string[] layers)
        {
            _table.Clear();
            Language = string.IsNullOrEmpty(language) ? "en" : language;
            foreach (var json in layers) Merge(json);
            OnLanguageChanged?.Invoke();
        }

        public void Merge(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            Dictionary<string, string> dict;
            try { dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json); }
            catch (JsonException) { return; }
            if (dict == null) return;
            foreach (var kv in dict)
                if (kv.Value != null) _table[kv.Key] = kv.Value;
        }

        public void Set(string key, string value) => _table[key] = value;

        public bool Has(string key) => _table.ContainsKey(key);

        public string T(string key) => key != null && _table.TryGetValue(key, out var v) ? v : key;

        public string T(string key, params object[] args)
        {
            string fmt = T(key);
            try { return string.Format(fmt, args); }
            catch (FormatException) { return fmt; }
        }

        public IReadOnlyDictionary<string, string> All => _table;
    }
}
