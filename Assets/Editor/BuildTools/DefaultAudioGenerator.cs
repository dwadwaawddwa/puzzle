using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuzzleStudio.EditorTools.BuildTools
{
    /// <summary>
    /// Synthesizes the built-in, royalty-free sounds and ambient music loops into Assets/_Project/Resources/Audio
    /// (WAV sources, imported as Vorbis). Menu: Build > Generate Default Audio.
    /// </summary>
    public static class DefaultAudioGenerator
    {
        const int SR = 44100;
        const string Dir = "Assets/_Project/Resources/Audio";

        [MenuItem("Build/Generate Default Audio", priority = 51)]
        public static void Generate()
        {
            Directory.CreateDirectory(Dir);

            WriteMono("click", Normalize(Click(), 0.55f));
            WriteMono("pick", Normalize(Pick(), 0.5f));
            WriteMono("drop", Normalize(Drop(), 0.6f));
            WriteMono("snap", Normalize(Snap(), 0.55f));
            WriteMono("star", Normalize(Star(), 0.6f));
            WriteMono("victory", Normalize(Victory(), 0.7f));
            WriteMono("locked", Normalize(Locked(), 0.55f));
            WriteMono("hint", Normalize(Hint(), 0.5f));
            WriteMono("undo", Normalize(Undo(), 0.45f));
            WriteMono("swoosh", Normalize(Swoosh(), 0.35f));

            WriteStereo("calm_01", Music(new MusicSpec
            {
                Seed = 11, Bpm = 72, Root = 60,
                Chords = new[] { C(60, 64, 67, 71), C(57, 60, 64, 67), C(53, 57, 60, 64), C(55, 59, 62, 64) },
                Scale = new[] { 0, 2, 4, 7, 9 },
            }));
            WriteStereo("calm_02", Music(new MusicSpec
            {
                Seed = 23, Bpm = 80, Root = 65,
                Chords = new[] { C(53, 57, 60, 64, 67), C(50, 53, 57, 60), C(58, 62, 65, 69), C(48, 52, 55, 57) },
                Scale = new[] { 0, 2, 4, 7, 9 },
            }));
            WriteStereo("calm_03", Music(new MusicSpec
            {
                Seed = 37, Bpm = 66, Root = 57,
                Chords = new[] { C(57, 60, 64, 67, 71), C(53, 57, 60, 64), C(48, 52, 55, 59), C(52, 55, 59, 62) },
                Scale = new[] { 0, 3, 5, 7, 10 },
            }));

            AssetDatabase.Refresh();
            ConfigureImporters();
            Debug.Log("[Audio] Default sounds and music generated.");
        }

        public static void GenerateBatch()
        {
            try { Generate(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) continue;
                bool music = Path.GetFileName(path).StartsWith("calm_");
                var s = imp.defaultSampleSettings;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = music ? 0.55f : 0.7f;
                s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                imp.defaultSampleSettings = s;
                imp.forceToMono = !music;
                imp.loadInBackground = music;
                imp.SaveAndReimport();
            }
        }

        // ================================================================== SFX

        static float[] Buffer(float seconds) => new float[(int)(seconds * SR)];

        static float[] Click()
        {
            var b = Buffer(0.08f);
            var rng = new System.Random(1);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] = Mathf.Sin(2 * Mathf.PI * 1700 * t) * Mathf.Exp(-t * 70) * 0.8f
                     + (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 500) * 0.3f;
            }
            return b;
        }

        static float[] Pick()
        {
            var b = Buffer(0.14f);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(520, 900, 1 - Mathf.Exp(-t * 30));
                phase += 2 * Math.PI * f / SR;
                float env = Mathf.Min(1, t / 0.004f) * Mathf.Exp(-t * 26);
                b[i] = ((float)Math.Sin(phase) + 0.2f * (float)Math.Sin(2 * phase)) * env;
            }
            return b;
        }

        static float[] Drop()
        {
            var b = Buffer(0.18f);
            double phase = 0;
            var rng = new System.Random(2);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(430, 230, 1 - Mathf.Exp(-t * 25));
                phase += 2 * Math.PI * f / SR;
                float env = Mathf.Min(1, t / 0.003f) * Mathf.Exp(-t * 24);
                lp += 0.08f * ((float)(rng.NextDouble() * 2 - 1) - lp);
                b[i] = (float)Math.Sin(phase) * env + lp * Mathf.Exp(-t * 60) * 0.8f;
            }
            return b;
        }

        static float[] Bell(float seconds, float freq, float[] ratios, float[] amps, float[] decays, float attack = 0.002f)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float a = Mathf.Min(1, t / attack);
                float s = 0;
                for (int k = 0; k < ratios.Length; k++)
                    s += amps[k] * Mathf.Sin(2 * Mathf.PI * freq * ratios[k] * t) * Mathf.Exp(-t * decays[k]);
                b[i] = s * a;
            }
            return b;
        }

        static float[] Snap()
        {
            var bell = Bell(0.55f, 1318.5f, new[] { 1f, 2f, 3.01f, 4.2f }, new[] { 1f, 0.35f, 0.18f, 0.08f }, new[] { 7f, 10f, 14f, 18f });
            var pop = Pick();
            for (int i = 0; i < pop.Length && i < bell.Length; i++) bell[i] += pop[i] * 0.35f;
            return bell;
        }

        static float[] Star()
        {
            var b = Bell(0.8f, 1568f, new[] { 1f, 1.5f, 2f, 3f }, new[] { 1f, 0.3f, 0.3f, 0.12f }, new[] { 4.5f, 6f, 7f, 10f });
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] *= 1f + 0.08f * Mathf.Sin(2 * Mathf.PI * 9 * t);
            }
            return b;
        }

        static float[] Victory()
        {
            var b = Buffer(2.6f);
            float[] notes = { 72, 76, 79, 84, 88 };
            for (int n = 0; n < notes.Length; n++)
            {
                var bell = Bell(1.6f, Hz(notes[n]), new[] { 1f, 2f, 3f }, new[] { 1f, 0.3f, 0.1f }, new[] { 3f, 5f, 8f });
                Mix(b, bell, (int)(n * 0.11f * SR), 0.55f);
            }
            // Warm final chord
            int start = (int)(0.55f * SR);
            float[] chord = { 60, 64, 67, 72 };
            for (int i = start; i < b.Length; i++)
            {
                float t = (i - start) / (float)SR;
                float env = Mathf.Min(1, t / 0.25f) * Mathf.Exp(-t * 1.3f);
                float s = 0;
                foreach (var m in chord) s += Mathf.Sin(2 * Mathf.PI * Hz(m) * t) + 0.25f * Mathf.Sin(4 * Mathf.PI * Hz(m) * t);
                b[i] += s * env * 0.12f;
            }
            return b;
        }

        static float[] Locked()
        {
            var b = Buffer(0.28f);
            foreach (float at in new[] { 0f, 0.1f })
            {
                int s0 = (int)(at * SR);
                float lp = 0;
                for (int i = s0; i < b.Length; i++)
                {
                    float t = (i - s0) / (float)SR;
                    float sq = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 165 * t));
                    lp += 0.06f * (sq - lp);
                    b[i] += lp * Mathf.Exp(-t * 35) * 0.9f;
                }
            }
            return b;
        }

        static float[] Hint()
        {
            var b = Buffer(0.7f);
            Mix(b, Bell(0.5f, Hz(81), new[] { 1f, 2f }, new[] { 1f, 0.25f }, new[] { 6f, 9f }), 0, 0.6f);
            Mix(b, Bell(0.55f, Hz(88), new[] { 1f, 2f }, new[] { 1f, 0.25f }, new[] { 5f, 8f }), (int)(0.09f * SR), 0.6f);
            return b;
        }

        static float[] Undo()
        {
            var b = Buffer(0.13f);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(880, 500, 1 - Mathf.Exp(-t * 30));
                phase += 2 * Math.PI * f / SR;
                b[i] = (float)Math.Sin(phase) * Mathf.Min(1, t / 0.004f) * Mathf.Exp(-t * 28);
            }
            return b;
        }

        static float[] Swoosh()
        {
            var b = Buffer(0.4f);
            var rng = new System.Random(3);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float p = t / 0.4f;
                float cutoff = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(p * Mathf.PI));
                lp += cutoff * ((float)(rng.NextDouble() * 2 - 1) - lp);
                lp2 += cutoff * (lp - lp2);
                b[i] = (lp - lp2 * 0.5f) * Mathf.Sin(p * Mathf.PI);
            }
            return b;
        }

        // ================================================================== music

        sealed class MusicSpec
        {
            public int Seed;
            public float Bpm;
            public int Root;
            public int[][] Chords;
            public int[] Scale;
        }

        static int[] C(params int[] notes) => notes;

        /// <summary>
        /// Ambient loop: soft detuned pads, a gentle bass, a pentatonic pluck melody and occasional chimes,
        /// through a stereo delay and a small reverb. Rendered twice and the second pass kept, so the reverb
        /// tail wraps around and the loop is seamless.
        /// </summary>
        static float[][] Music(MusicSpec spec)
        {
            var rng = new System.Random(spec.Seed);
            double beat = 60.0 / spec.Bpm;
            int beatsPerChord = 8;
            int chordsPerLoop = spec.Chords.Length * 2;
            double loopSec = chordsPerLoop * beatsPerChord * beat;
            int loop = (int)(loopSec * SR);
            int total = loop * 2;
            var dryL = new float[total];
            var dryR = new float[total];
            var sendL = new float[total];
            var sendR = new float[total];

            // Melody / chime events for ONE loop (repeated identically in the second).
            var events = new List<(double time, float midi, float amp, float pan, bool chime)>();
            int degree = 2;
            int eighths = chordsPerLoop * beatsPerChord * 2;
            for (int e = 0; e < eighths; e++)
            {
                bool downbeat = e % 4 == 0;
                double chance = downbeat ? 0.65 : 0.32;
                if (rng.NextDouble() > chance) continue;
                degree = Math.Max(0, Math.Min(spec.Scale.Length * 2 - 1, degree + rng.Next(-2, 3)));
                int octave = degree / spec.Scale.Length;
                float midi = spec.Root + 12 + spec.Scale[degree % spec.Scale.Length] + 12 * octave;
                events.Add((e * beat / 2, midi, downbeat ? 0.13f : 0.09f, (float)(rng.NextDouble() * 1.2 - 0.6), false));
            }
            for (int c = 0; c < chordsPerLoop; c += 2)
            {
                if (rng.NextDouble() < 0.6)
                {
                    float midi = spec.Root + 24 + spec.Scale[rng.Next(spec.Scale.Length)];
                    events.Add((c * beatsPerChord * beat + rng.Next(4) * beat, midi, 0.05f, (float)(rng.NextDouble() * 1.6 - 0.8), true));
                }
            }

            for (int pass = 0; pass < 2; pass++)
            {
                double offset = pass * loopSec;
                for (int c = 0; c < chordsPerLoop; c++)
                {
                    var chord = spec.Chords[c % spec.Chords.Length];
                    double start = offset + c * beatsPerChord * beat;
                    double dur = beatsPerChord * beat;
                    // Pads
                    for (int n = 0; n < chord.Length; n++)
                    {
                        float pan = chord.Length > 1 ? Mathf.Lerp(-0.5f, 0.5f, n / (float)(chord.Length - 1)) : 0f;
                        Pad(dryL, dryR, sendL, sendR, start, dur, Hz(chord[n]), 0.045f, pan);
                    }
                    // Bass on beats 1 and 5
                    Pluck(dryL, dryR, sendL, sendR, start, Hz(chord[0] - 12), 0.16f, 0f, 2.2f, 0.1f);
                    Pluck(dryL, dryR, sendL, sendR, start + 4 * beat, Hz(chord[0] - 12), 0.11f, 0f, 2.2f, 0.1f);
                }
                foreach (var ev in events)
                {
                    if (ev.chime) Chime(dryL, dryR, sendL, sendR, offset + ev.time, Hz(ev.midi), ev.amp, ev.pan);
                    else Pluck(dryL, dryR, sendL, sendR, offset + ev.time, Hz(ev.midi), ev.amp, ev.pan, 3.2f, 0.6f);
                }
            }

            ApplyDelay(sendL, sendR, (int)(beat * 0.75 * SR), 0.38f);
            ApplyReverb(sendL, sendR);

            var outL = new float[loop];
            var outR = new float[loop];
            for (int i = 0; i < loop; i++)
            {
                outL[i] = dryL[loop + i] + sendL[loop + i] * 0.55f;
                outR[i] = dryR[loop + i] + sendR[loop + i] * 0.55f;
            }
            float peak = 0;
            for (int i = 0; i < loop; i++) peak = Mathf.Max(peak, Mathf.Abs(outL[i]), Mathf.Abs(outR[i]));
            float k = peak > 0 ? 0.62f / peak : 1f;
            for (int i = 0; i < loop; i++)
            {
                outL[i] = (float)Math.Tanh(outL[i] * k * 1.1f);
                outR[i] = (float)Math.Tanh(outR[i] * k * 1.1f);
            }
            return new[] { outL, outR };
        }

        static void Pad(float[] l, float[] r, float[] sl, float[] sr, double start, double dur, float freq, float amp, float pan)
        {
            const double attack = 1.4, release = 1.8;
            int s0 = (int)(start * SR), s1 = Math.Min(l.Length, (int)((start + dur + release) * SR));
            float gl = amp * Mathf.Sqrt(0.5f - pan * 0.5f), gr = amp * Mathf.Sqrt(0.5f + pan * 0.5f);
            double f1 = freq * 1.0035, f2 = freq * 0.9965;
            float lp = 0;
            for (int i = Math.Max(0, s0); i < s1; i++)
            {
                double t = (i - s0) / (double)SR;
                double env = t < attack ? t / attack : t < dur ? 1 : Math.Max(0, 1 - (t - dur) / release);
                env = env * env * (3 - 2 * env); // smoothstep
                double a = 2 * Math.PI * t;
                double s = Math.Sin(a * f1) + Math.Sin(a * f2)
                         + 0.32 * (Math.Sin(2 * a * f1) + Math.Sin(2 * a * f2))
                         + 0.1 * Math.Sin(3 * a * freq);
                s *= 1 + 0.06 * Math.Sin(2 * Math.PI * 0.23 * t);
                lp += 0.18f * ((float)s - lp);
                float v = lp * (float)env;
                l[i] += v * gl; r[i] += v * gr;
                sl[i] += v * gl * 0.6f; sr[i] += v * gr * 0.6f;
            }
        }

        static void Pluck(float[] l, float[] r, float[] sl, float[] sr, double start, float freq, float amp, float pan, float decay, float send)
        {
            int s0 = (int)(start * SR);
            int len = (int)(Math.Min(4.0, 6.0 / decay) * SR);
            float gl = amp * Mathf.Sqrt(0.5f - pan * 0.5f), gr = amp * Mathf.Sqrt(0.5f + pan * 0.5f);
            for (int i = 0; i < len && s0 + i < l.Length; i++)
            {
                if (s0 + i < 0) continue;
                double t = i / (double)SR;
                double a = 2 * Math.PI * freq * t;
                double s = Math.Sin(a) * Math.Exp(-t * decay)
                         + 0.25 * Math.Sin(2 * a) * Math.Exp(-t * decay * 1.8)
                         + 0.08 * Math.Sin(3 * a) * Math.Exp(-t * decay * 2.6);
                s *= Math.Min(1, t / 0.005);
                float v = (float)s;
                l[s0 + i] += v * gl; r[s0 + i] += v * gr;
                sl[s0 + i] += v * gl * send; sr[s0 + i] += v * gr * send;
            }
        }

        static void Chime(float[] l, float[] r, float[] sl, float[] sr, double start, float freq, float amp, float pan)
        {
            int s0 = (int)(start * SR);
            int len = 3 * SR;
            float gl = amp * Mathf.Sqrt(0.5f - pan * 0.5f), gr = amp * Mathf.Sqrt(0.5f + pan * 0.5f);
            for (int i = 0; i < len && s0 + i < l.Length; i++)
            {
                double t = i / (double)SR;
                double a = 2 * Math.PI * freq * t;
                double s = Math.Sin(a) * Math.Exp(-t * 1.6) + 0.3 * Math.Sin(2.76 * a) * Math.Exp(-t * 3) + 0.12 * Math.Sin(5.4 * a) * Math.Exp(-t * 5);
                s *= Math.Min(1, t / 0.003);
                float v = (float)s;
                l[s0 + i] += v * gl * 0.5f; r[s0 + i] += v * gr * 0.5f;
                sl[s0 + i] += v * gl; sr[s0 + i] += v * gr;
            }
        }

        static void ApplyDelay(float[] l, float[] r, int delay, float feedback)
        {
            var bl = new float[delay];
            var br = new float[delay];
            int p = 0;
            for (int i = 0; i < l.Length; i++)
            {
                float dl = bl[p], dr = br[p];
                // Ping-pong
                bl[p] = l[i] + dr * feedback;
                br[p] = r[i] + dl * feedback;
                l[i] += dl * 0.5f;
                r[i] += dr * 0.5f;
                p = (p + 1) % delay;
            }
        }

        static void ApplyReverb(float[] l, float[] r)
        {
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491 };
            int[] allpasses = { 556, 441, 341 };
            ReverbChannel(l, combs, allpasses, 0, out var wetL);
            ReverbChannel(r, combs, allpasses, 23, out var wetR);
            for (int i = 0; i < wetL.Length; i++) { l[i] = wetL[i]; }
            for (int i = 0; i < wetR.Length; i++) { r[i] = wetR[i]; }
        }

        static void ReverbChannel(float[] input, int[] combs, int[] allpasses, int spread, out float[] output)
        {
            output = new float[input.Length];
            var combBuf = new float[combs.Length][];
            var combIdx = new int[combs.Length];
            var combStore = new float[combs.Length];
            for (int c = 0; c < combs.Length; c++) combBuf[c] = new float[combs[c] + spread];
            var apBuf = new float[allpasses.Length][];
            var apIdx = new int[allpasses.Length];
            for (int a = 0; a < allpasses.Length; a++) apBuf[a] = new float[allpasses[a] + spread];
            const float feedback = 0.84f, damp = 0.25f;

            for (int i = 0; i < input.Length; i++)
            {
                float x = input[i] * 0.02f;
                float sum = 0;
                for (int c = 0; c < combs.Length; c++)
                {
                    var buf = combBuf[c];
                    float y = buf[combIdx[c]];
                    combStore[c] = y * (1 - damp) + combStore[c] * damp;
                    buf[combIdx[c]] = x + combStore[c] * feedback;
                    combIdx[c] = (combIdx[c] + 1) % buf.Length;
                    sum += y;
                }
                for (int a = 0; a < allpasses.Length; a++)
                {
                    var buf = apBuf[a];
                    float bufOut = buf[apIdx[a]];
                    buf[apIdx[a]] = sum + bufOut * 0.5f;
                    sum = bufOut - sum;
                    apIdx[a] = (apIdx[a] + 1) % buf.Length;
                }
                output[i] = input[i] + sum * 3f;
            }
        }

        // ================================================================== helpers

        static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        static void Mix(float[] into, float[] src, int offset, float gain)
        {
            for (int i = 0; i < src.Length && offset + i < into.Length; i++) into[offset + i] += src[i] * gain;
        }

        static float[] Normalize(float[] b, float peakTarget)
        {
            float peak = 0;
            foreach (var v in b) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak <= 0) return b;
            float k = peakTarget / peak;
            // 4 ms fade-out to avoid clicks
            int fade = Math.Min(b.Length, SR / 250);
            for (int i = 0; i < b.Length; i++)
            {
                float f = i >= b.Length - fade ? (b.Length - i) / (float)fade : 1f;
                b[i] *= k * f;
            }
            return b;
        }

        static void WriteMono(string name, float[] data) => WriteWav(Path.Combine(Dir, name + ".wav"), new[] { data });
        static void WriteStereo(string name, float[][] data) => WriteWav(Path.Combine(Dir, name + ".wav"), data);

        static void WriteWav(string path, float[][] channels)
        {
            int ch = channels.Length, n = channels[0].Length;
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);
            int dataBytes = n * ch * 2;
            bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            bw.Write(36 + dataBytes);
            bw.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            bw.Write(16); bw.Write((short)1); bw.Write((short)ch);
            bw.Write(SR); bw.Write(SR * ch * 2); bw.Write((short)(ch * 2)); bw.Write((short)16);
            bw.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            bw.Write(dataBytes);
            for (int i = 0; i < n; i++)
                for (int c = 0; c < ch; c++)
                    bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(channels[c][i] * 32767f), -32768, 32767));
        }
    }
}
