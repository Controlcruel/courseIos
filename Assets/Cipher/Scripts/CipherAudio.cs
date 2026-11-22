using System.Collections.Generic;
using UnityEngine;

namespace Cipher
{
    /// <summary>Звуки генерируются программно — аудиофайлы не нужны.</summary>
    public static class CipherAudio
    {
        static AudioSource source;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static void Init(Transform listener)
        {
            if (source) return;
            source = listener.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.6f;
        }

        public static void Hover() => Play("hover", new[] { 1400f }, 0.04f, 0.25f);
        public static void Scan() => Play("scan", new[] { 520f, 780f, 1040f }, 0.09f, 0.5f);
        public static void Collect() => Play("collect", new[] { 880f, 1320f }, 0.08f, 0.5f);
        public static void Select() => Play("select", new[] { 660f }, 0.07f, 0.4f);
        public static void Error() => Play("error", new[] { 220f, 180f }, 0.14f, 0.5f);
        public static void Success() => Play("success", new[] { 523f, 659f, 784f, 1047f }, 0.12f, 0.6f);
        public static void Portal() => Play("portal", new[] { 196f, 262f, 330f, 392f, 523f, 659f }, 0.18f, 0.6f);

        static void Play(string key, float[] notes, float noteLen, float vol)
        {
            if (!source) return;
            if (!cache.TryGetValue(key, out var clip)) cache[key] = clip = Make(key, notes, noteLen);
            source.PlayOneShot(clip, vol);
        }

        static AudioClip Make(string name, float[] notes, float noteLen)
        {
            const int rate = 44100;
            int per = (int)(rate * noteLen);
            var data = new float[per * notes.Length + rate / 10];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < per * 2 && n * per + i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Min(1f, i / 200f);
                    data[n * per + i] += (Mathf.Sin(2 * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * notes[n] * t)) * env * 0.5f;
                }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
