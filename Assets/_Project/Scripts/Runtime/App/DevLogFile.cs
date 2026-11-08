using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TowerGlide.App
{
    /// <summary>
    /// 개발 빌드 전용 로그 파일(실기기 테스트 중 생긴 경고·오류·예외를 나중에 adb로 가져가기 위함).
    /// 위치: persistentDataPath/logs/tg-yyyyMMdd-HHmmss.log, 최근 5개만 유지, 파일당 2MB 상한.
    /// 기기 밖으로 보내지 않는다(서버 없음). 릴리스 빌드에서는 만들지 않는다.
    /// 가져오기: adb shell run-as com.logicphantom.towerglide ls files/logs  (docs/DEVICE_TEST_GUIDE.md)
    /// </summary>
    public sealed class DevLogFile : IDisposable
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private const int KeepFiles = 5;
        private readonly object _lock = new object();
        private StreamWriter _writer;
        private long _bytes;

        public string Path { get; }

        public DevLogFile(string directory)
        {
            Directory.CreateDirectory(directory);
            Prune(directory);
            Path = System.IO.Path.Combine(directory, $"tg-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            _writer = new StreamWriter(Path, false, new UTF8Encoding(false)) { AutoFlush = true };
            Write($"TOWERGLIDE {Application.version} | {SystemInfo.deviceModel} | {SystemInfo.operatingSystem} | " +
                  $"RAM {SystemInfo.systemMemorySize}MB | GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}) | " +
                  $"{Screen.width}x{Screen.height} @{Screen.currentResolution.refreshRateRatio.value:0}Hz");
            Application.logMessageReceivedThreaded += OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            // 일반 Log는 양이 많아 경고 이상만 + [TOWERGLIDE]/[Streaming] 태그가 붙은 로그
            bool tagged = condition != null && (condition.StartsWith("[TOWERGLIDE]") || condition.StartsWith("[Streaming]"));
            if (type == LogType.Log && !tagged) return;
            string line = $"{DateTime.Now:HH:mm:ss.fff} {type}: {condition}";
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) line += "\n" + stackTrace;
            Write(line);
        }

        private void Write(string line)
        {
            lock (_lock)
            {
                if (_writer == null || _bytes > MaxBytes) return;
                _writer.WriteLine(line);
                _bytes += line.Length + 1;
                if (_bytes > MaxBytes) _writer.WriteLine("... (2MB 상한 도달, 이후 기록 생략)");
            }
        }

        private static void Prune(string directory)
        {
            try
            {
                string[] files = Directory.GetFiles(directory, "tg-*.log");
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length - (KeepFiles - 1); i++) File.Delete(files[i]);
            }
            catch (Exception)
            {
                // 정리 실패는 무시(로그 기능이 게임을 막지 않음)
            }
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
