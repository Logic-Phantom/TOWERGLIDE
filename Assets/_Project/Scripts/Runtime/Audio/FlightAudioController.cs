using TowerGlide.App;
using TowerGlide.Core.App;
using TowerGlide.Core.Audio;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using UnityEngine;

namespace TowerGlide.Audio
{
    /// <summary>
    /// 비행 중 엔진·바람 루프(명세 Phase 3 "엔진/바람 사운드"). 기종별 엔진음(AircraftDefinition.engineSound),
    /// 속도·스로틀·부스트에 따라 음높이·음량이 변한다(Core EngineSoundModel). 비행 중이 아니면 줄인다.
    /// </summary>
    public sealed class FlightAudioController : MonoBehaviour
    {
        [SerializeField] private FlightSession session;
        [SerializeField] private float smoothing = 6f;

        private AudioSource _engine;
        private AudioSource _wind;
        private EngineSoundKind _kind;
        private bool _wasBoosting;
        private bool _ready;

        private void Start()
        {
            AudioManager audio = GameApp.Instance != null ? GameApp.Instance.Audio : null;
            if (audio == null || session == null) return;

            AircraftDefinition def = session.AircraftDefinition;
            _kind = def != null ? def.engineSound : EngineSoundKind.Propeller;
            _engine = CreateLoop("Engine", audio.GetEngineLoop(_kind));
            _wind = CreateLoop("Wind", audio.GetWindLoop());
            _ready = true;
        }

        private AudioSource CreateLoop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.Play();
            return s;
        }

        private void Update()
        {
            if (!_ready) return;
            AircraftFlightController a = session.Aircraft;
            float sfx = GameApp.Instance != null ? GameApp.Instance.Settings.sfxVolume : 1f;
            bool flying = session.State == GameState.Flying && a != null && !session.IsWaitingForWorld;

            float engineVol = 0f, windVol = 0f, pitch = 1f;
            if (a != null && a.Tuning != null)
            {
                float top = a.Tuning.boostEnabled ? a.Tuning.boostMaxSpeed : a.Tuning.maxSpeed;
                float s01 = EngineSoundModel.Speed01(a.Speed, a.Tuning.minFlightSpeed * 0.5f, top);
                float throttle = session.LastThrottle;
                pitch = EngineSoundModel.Pitch(_kind, s01, throttle, a.IsBoosting);
                engineVol = EngineSoundModel.EngineVolume(s01, throttle, a.IsBoosting);
                windVol = EngineSoundModel.WindVolume(s01);

                if (flying && a.IsBoosting && !_wasBoosting) GameApp.Instance?.Audio?.PlaySfx(SfxKind.BoostStart, 0.7f);
                _wasBoosting = a.IsBoosting;
            }
            if (!flying)
            {
                engineVol *= 0.25f;
                windVol = 0f;
            }

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _engine.volume = Mathf.Lerp(_engine.volume, engineVol * 0.6f * sfx, k);
            _engine.pitch = Mathf.Lerp(_engine.pitch, pitch, k);
            _wind.volume = Mathf.Lerp(_wind.volume, windVol * 0.5f * sfx, k);
        }
    }
}
