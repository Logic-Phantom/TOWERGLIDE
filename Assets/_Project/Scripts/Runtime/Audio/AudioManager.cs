using System.Collections.Generic;
using TowerGlide.Core.Audio;
using UnityEngine;

namespace TowerGlide.Audio
{
    /// <summary>
    /// 앱 전역 효과음 + 절차 합성 클립 캐시(Core ProceduralAudio → AudioClip). GameApp이 소유(씬 전환에도 유지).
    /// - 마스터 볼륨 = AudioListener.volume(GameApp.ApplySettings), 효과음 볼륨 = SfxVolume.
    /// - 일시정지 시 AudioListener.pause로 함께 멈춘다. 효과음 소스는 8개 순환(동시 재생 상한 — 과한 소리 방지).
    /// - 음악(musicVolume)은 음원이 없어 아직 쓰지 않는다(Phase 4에서 라이선스 확인된 음원 또는 합성 앰비언트).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int Voices = 8;

        private readonly Dictionary<SfxKind, AudioClip> _sfx = new Dictionary<SfxKind, AudioClip>();
        private readonly Dictionary<EngineSoundKind, AudioClip> _engines = new Dictionary<EngineSoundKind, AudioClip>();
        private AudioClip _wind;
        private AudioSource[] _voices;
        private int _next;
        private float _lastBoostSfx = -10f;

        public float SfxVolume { get; set; } = 1f;

        private void Awake()
        {
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f; // 2D — 모바일에서 명확하게
                _voices[i] = s;
            }
        }

        public void PlaySfx(SfxKind kind, float volume = 1f, float pitch = 1f)
        {
            if (_voices == null) return;
            if (kind == SfxKind.BoostStart)
            {
                if (Time.unscaledTime - _lastBoostSfx < 0.5f) return;
                _lastBoostSfx = Time.unscaledTime;
            }
            AudioSource src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = pitch;
            src.PlayOneShot(GetSfx(kind), Mathf.Clamp01(volume * SfxVolume));
        }

        /// <summary>UI 버튼에서 호출(인자 없는 persistent listener용).</summary>
        public void PlayClick() => PlaySfx(SfxKind.UiClick, 0.8f);

        public AudioClip GetSfx(SfxKind kind)
        {
            if (!_sfx.TryGetValue(kind, out AudioClip c) || c == null)
                _sfx[kind] = c = Create($"sfx_{kind}", ProceduralAudio.Sfx(kind));
            return c;
        }

        public AudioClip GetEngineLoop(EngineSoundKind kind)
        {
            if (!_engines.TryGetValue(kind, out AudioClip c) || c == null)
                _engines[kind] = c = Create($"engine_{kind}", ProceduralAudio.EngineLoop(kind));
            return c;
        }

        public AudioClip GetWindLoop() => _wind != null ? _wind : _wind = Create("wind", ProceduralAudio.WindLoop());

        private static AudioClip Create(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, ProceduralAudio.DefaultSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
