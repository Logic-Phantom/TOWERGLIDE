using UnityEngine;

namespace TowerGlide.Platform
{
    public enum HapticStrength
    {
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// 플랫폼 진동 추상화. 설정에서 끄면 아무것도 하지 않는다.
    /// Android: VibrationEffect(API 26+) 원샷, 실패 시 Handheld.Vibrate.
    ///   (Handheld.Vibrate 참조가 있어야 Unity가 VIBRATE 권한을 자동 추가한다 — 일반 권한, 런타임 동의 없음)
    /// iOS: Handheld.Vibrate(시스템 진동). 정교한 탭틱은 후속(Core Haptics 네이티브 플러그인 필요).
    /// </summary>
    public interface IHapticService
    {
        bool Enabled { get; set; }
        void Play(HapticStrength strength);
    }

    public sealed class HapticService : IHapticService
    {
        public bool Enabled { get; set; } = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private bool _initialized;
        private int _sdkInt;
#endif

        public void Play(HapticStrength strength)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(strength);
#elif UNITY_IOS && !UNITY_EDITOR
            if (strength != HapticStrength.Light) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PlayAndroid(HapticStrength strength)
        {
            long ms = strength == HapticStrength.Light ? 15 : strength == HapticStrength.Medium ? 35 : 70;
            int amplitude = strength == HapticStrength.Light ? 60 : strength == HapticStrength.Medium ? 140 : 255;
            try
            {
                if (!_initialized)
                {
                    _initialized = true;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdkInt = version.GetStatic<int>("SDK_INT");
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (_vibrator == null) { Handheld.Vibrate(); return; }
                if (_sdkInt >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
        }
#endif
    }
}
