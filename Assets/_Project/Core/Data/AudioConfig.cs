using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Data
{
    [Serializable]
    public sealed class AudioConfig
    {
        /// <summary>"default:calm_01" (built-in) or a pack-relative path ("audio/music_menu.ogg").</summary>
        public string musicMenu = "default:calm_01";
        public string musicGame = "default:calm_02";
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        public float crossfadeSeconds = 1.5f;
        public Dictionary<string, string> sfx = DefaultSfx();
        public float pitchVariation = 0.08f;

        public static Dictionary<string, string> DefaultSfx() => new Dictionary<string, string>
        {
            { "click", "default:click" },
            { "pick", "default:pick" },
            { "drop", "default:drop" },
            { "snap", "default:snap" },
            { "victory", "default:victory" },
            { "star", "default:star" },
            { "locked", "default:locked" },
        };
    }
}
