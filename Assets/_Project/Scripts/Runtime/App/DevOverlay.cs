using System.Text;
using TowerGlide.Core.App;
using TowerGlide.Core.Save;
using TowerGlide.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace TowerGlide.App
{
    /// <summary>
    /// 실기기 측정 오버레이(개발 빌드 또는 성능 측정 빌드 TG_PERF_HUD 전용). docs/PERFORMANCE.md·SEOUL_WORLD_PERFORMANCE.md 기록용.
    /// 표시: 평균 FPS·1% Low·최대 프레임·30fps 미만 비율·메모리·기기·GPU·해상도·품질·청크 상태.
    /// 켜기/끄기: 화면을 세 손가락으로 탭(또는 F2). "복사"는 현재 값을 클립보드로(문서 표에 붙여 넣기).
    /// OnGUI 기반이라 어느 씬에서나 동작하고 씬 설정이 필요 없다.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        private readonly FrameStats _stats = new FrameStats(600);
        private readonly StringBuilder _sb = new StringBuilder(512);
        private bool _visible = true;
        private float _refresh;
        private string _text = string.Empty;
        private GUIStyle _style;
        private bool _threeFingerLatch;

        public static bool Enabled
        {
            get
            {
#if TG_PERF_HUD
                return true;
#else
                return Debug.isDebugBuild;
#endif
            }
        }

        private void Update()
        {
            _stats.Add(Time.unscaledDeltaTime);

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) _visible = !_visible;
            int touches = Touchscreen.current != null ? CountTouches() : 0;
            if (touches >= 3 && !_threeFingerLatch)
            {
                _visible = !_visible;
                _threeFingerLatch = true;
            }
            else if (touches == 0) _threeFingerLatch = false;

            _refresh += Time.unscaledDeltaTime;
            if (_refresh >= 0.5f && _visible)
            {
                _refresh = 0f;
                _text = Build();
            }
        }

        private static int CountTouches()
        {
            int n = 0;
            foreach (var t in Touchscreen.current.touches)
                if (t.press.isPressed) n++;
            return n;
        }

        private string Build()
        {
            GameApp app = GameApp.Instance;
            SettingsData s = app != null ? app.Settings : null;
            _sb.Clear();
            _sb.Append("FPS ").Append(_stats.AverageFps.ToString("0.0"))
               .Append("  1%Low ").Append(_stats.OnePercentLowFps.ToString("0.0"))
               .Append("  max ").Append(_stats.MaxFrameMs.ToString("0")).Append("ms")
               .Append("  <30fps ").Append((_stats.FractionSlowerThan(30f) * 100f).ToString("0.0")).Append('%').Append('\n');
            _sb.Append("MEM alloc ").Append(Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024))
               .Append("MB  reserved ").Append(Profiler.GetTotalReservedMemoryLong() / (1024 * 1024))
               .Append("MB  mono ").Append(Profiler.GetMonoUsedSizeLong() / (1024 * 1024)).Append("MB\n");
            _sb.Append(SystemInfo.deviceModel).Append(" | RAM ").Append(SystemInfo.systemMemorySize).Append("MB | ")
               .Append(SystemInfo.graphicsDeviceName).Append(" (").Append(SystemInfo.graphicsDeviceType).Append(")\n");
            _sb.Append(Screen.width).Append('x').Append(Screen.height);
            if (s != null)
                _sb.Append(" | ").Append(SettingsOptions.QualityLabel(s.qualityTier)).Append(" | target ").Append(s.targetFrameRate).Append("fps");
            WorldStreamingManager streaming = FindFirstObjectByType<WorldStreamingManager>();
            if (streaming != null && Camera.main != null)
                _sb.Append('\n').Append(streaming.DebugSummary(Camera.main.transform.position));
            return _sb.ToString();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true };
                _style.normal.textColor = new Color(0.8f, 1f, 0.8f);
            }
            _style.fontSize = Mathf.RoundToInt(13 * scale);
            var rect = new Rect(8 * scale, 8 * scale, 520 * scale, 120 * scale);
            GUI.Box(rect, _text, _style);
            if (GUI.Button(new Rect(rect.xMax + 6 * scale, rect.y, 70 * scale, 34 * scale), "복사"))
                GUIUtility.systemCopyBuffer = _text;
            if (GUI.Button(new Rect(rect.xMax + 6 * scale, rect.y + 40 * scale, 70 * scale, 34 * scale), "초기화"))
                _stats.Reset();
        }
    }
}
