using System.Collections.Generic;
using UnityEngine;

namespace GPA.UI
{
    // 효과음과 배경음. Art/Resources/Audio 아래 파일을 이름으로 부른다.
    public static class Sound
    {
        static AudioSource sfx, music;
        static readonly Dictionary<string, AudioClip> clips = new();

        public static void Init(GameObject host)
        {
            sfx = host.AddComponent<AudioSource>();
            music = host.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0.4f;
        }

        public static void Play(string name)
        {
            if (sfx == null) return;
            if (!clips.TryGetValue(name, out var clip) || clip == null)
                clips[name] = clip = Resources.Load<AudioClip>("Audio/Sfx/" + name);
            if (clip != null) sfx.PlayOneShot(clip, 0.7f);
        }

        public static void Music(string name)
        {
            if (music == null) return;
            var clip = Resources.Load<AudioClip>("Audio/Music/" + name);
            if (music.clip == clip) return;
            music.clip = clip;
            music.Play();
        }
    }
}
