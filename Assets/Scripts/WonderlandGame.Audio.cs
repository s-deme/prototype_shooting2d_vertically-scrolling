using System.Collections.Generic;
using UnityEngine;

namespace AliceMirrorfall
{
    public sealed partial class WonderlandGame
    {
        private AudioSource musicSource, sfxSource;
        private readonly Dictionary<string, AudioClip> toneCache = new Dictionary<string, AudioClip>();

        private void CreateAudio()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.clip = CreateMusicClip();
            RefreshVolumes();
        }

        private void RefreshVolumes()
        {
            float master = save.master / 100f;
            if (musicSource != null) musicSource.volume = master * save.music / 100f * .35f;
            if (sfxSource != null) sfxSource.volume = master * save.sfx / 100f * .23f;
        }

        private void StartMusic()
        {
            if (save.music > 0 && musicSource != null && !musicSource.isPlaying) musicSource.Play();
        }

        private void StopMusic()
        {
            if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
        }

        private void PlayTone(float frequency, float duration, float amplitude)
        {
            if (sfxSource == null || save.sfx <= 0) return;

            string key = frequency + ":" + duration + ":" + amplitude;
            if (!toneCache.TryGetValue(key, out AudioClip clip))
            {
                const int rate = 22050;
                int count = Mathf.Max(32, Mathf.CeilToInt(rate * duration));
                var data = new float[count];
                for (int i = 0; i < count; i++)
                {
                    data[i] = Mathf.Sin(i * frequency * Mathf.PI * 2 / rate) * Mathf.Exp(-i / (float)count * 5) * amplitude;
                }

                clip = AudioClip.Create("SFX-" + key, count, 1, rate, false);
                clip.SetData(data, 0);
                toneCache[key] = clip;
            }

            sfxSource.PlayOneShot(clip);
        }

        private static AudioClip CreateMusicClip()
        {
            const int rate = 22050;
            var data = new float[rate * 4];
            float[] notes = { 261.63f, 329.63f, 392f, 523.25f, 392f, 329.63f, 293.66f, 369.99f, 440f, 587.33f, 440f, 369.99f };

            for (int i = 0; i < data.Length; i++)
            {
                float time = i / (float)rate;
                float note = notes[Mathf.FloorToInt(time * 3) % notes.Length] * .5f;
                float phase = time % (1f / 3f);
                float envelope = Mathf.Clamp01(1 - phase * 3) * .42f;
                data[i] = (Mathf.Sin(Mathf.PI * 2 * note * time) + Mathf.Sin(Mathf.PI * 2 * note * 2 * time) * .18f) * envelope;
            }

            var clip = AudioClip.Create("Wonderland Theme", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (var clip in toneCache.Values)
            {
                if (clip != null) Destroy(clip);
            }
        }
    }
}
