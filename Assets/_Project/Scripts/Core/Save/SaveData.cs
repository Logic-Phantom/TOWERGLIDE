using System;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Save
{
    // JsonUtility 호환을 위해 public 필드 + [Serializable]만 사용한다(프로퍼티·Dictionary 금지).

    public enum QualityTier
    {
        Performance = 0,
        Balanced = 1,
        Quality = 2,
    }

    [Serializable]
    public sealed class SettingsData
    {
        public int qualityTier = (int)QualityTier.Balanced;
        public float cameraShake = 0.6f;      // 0..1
        public float controlSensitivity = 1f; // 0.5..1.5
        public bool invertPitch;
        public bool floatingJoystick = true;
        public bool gyroEnabled;              // 후속 기능, 기본 Off
        public float masterVolume = 1f;       // 0..1
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        public bool hapticsEnabled = true;
        public bool tutorialSeen;
        public int targetFrameRate = 60;      // 30 | 60
        /// <summary>첫 실행 기기 사양 기반 품질 자동 선택을 했는지(QualityAutoSelector). 없던 필드 → 기존 사용자는 false로 읽혀 한 번 적용.</summary>
        public bool qualityAutoApplied;

        public SettingsData Sanitized()
        {
            return new SettingsData
            {
                qualityTier = (int)FlightMath.Clamp(qualityTier, 0, 2),
                cameraShake = FlightMath.Clamp01(FlightMath.Sanitize(cameraShake)),
                controlSensitivity = FlightMath.Clamp(Finite(controlSensitivity, 1f), 0.5f, 1.5f),
                invertPitch = invertPitch,
                floatingJoystick = floatingJoystick,
                gyroEnabled = gyroEnabled,
                masterVolume = FlightMath.Clamp01(Finite(masterVolume, 1f)),
                musicVolume = FlightMath.Clamp01(Finite(musicVolume, 0.7f)),
                sfxVolume = FlightMath.Clamp01(Finite(sfxVolume, 1f)),
                hapticsEnabled = hapticsEnabled,
                tutorialSeen = tutorialSeen,
                targetFrameRate = targetFrameRate <= 30 ? 30 : 60,
                qualityAutoApplied = qualityAutoApplied,
            };
        }

        private static float Finite(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
    }

    [Serializable]
    public sealed class RecordsData
    {
        public int bestSkylineScore;
        /// <summary>완주 최고 기록(초). 0 = 기록 없음.</summary>
        public float bestSkylineSeconds;
        public float longestFlightMeters;
        public float topSpeedKmh;
        public float maxAltitudeMeters;
        public int totalRuns;
        // v2: 해금 조건용 누적 기록
        public float totalDistanceMeters;
        public float totalFlightSeconds;
        public int skylineCompletions;

        public RecordsData Sanitized()
        {
            return new RecordsData
            {
                bestSkylineScore = Math.Max(0, bestSkylineScore),
                bestSkylineSeconds = Math.Max(0f, FlightMath.Sanitize(bestSkylineSeconds)),
                longestFlightMeters = Math.Max(0f, FlightMath.Sanitize(longestFlightMeters)),
                topSpeedKmh = Math.Max(0f, FlightMath.Sanitize(topSpeedKmh)),
                maxAltitudeMeters = Math.Max(0f, FlightMath.Sanitize(maxAltitudeMeters)),
                totalRuns = Math.Max(0, totalRuns),
                totalDistanceMeters = Math.Max(0f, FlightMath.Sanitize(totalDistanceMeters)),
                totalFlightSeconds = Math.Max(0f, FlightMath.Sanitize(totalFlightSeconds)),
                skylineCompletions = Math.Max(0, skylineCompletions),
            };
        }

        /// <summary>런 결과를 기록에 반영한다.</summary>
        /// <returns>SKYLINE RUN 최고 점수 또는 최고 기록 갱신 여부</returns>
        public bool Apply(RunResult result)
        {
            totalRuns++;
            longestFlightMeters = Math.Max(longestFlightMeters, result.DistanceMeters);
            topSpeedKmh = Math.Max(topSpeedKmh, result.MaxSpeedKmh);
            maxAltitudeMeters = Math.Max(maxAltitudeMeters, result.MaxAltitudeMeters);
            totalDistanceMeters += Math.Max(0f, FlightMath.Sanitize(result.DistanceMeters));
            totalFlightSeconds += Math.Max(0f, FlightMath.Sanitize(result.FlightSeconds));

            if (result.Mode != FlightMode.SkylineRun) return false;

            if (result.Completed) skylineCompletions++;
            bool newBest = false;
            int total = result.Score.Total;
            if (total > bestSkylineScore)
            {
                bestSkylineScore = total;
                newBest = true;
            }
            if (result.Completed && result.FlightSeconds > 0f &&
                (bestSkylineSeconds <= 0f || result.FlightSeconds < bestSkylineSeconds))
            {
                bestSkylineSeconds = result.FlightSeconds;
                newBest = true;
            }
            return newBest;
        }
    }

    [Serializable]
    public sealed class SaveData
    {
        /// <summary>저장 형식 버전. 구조를 바꾸면 올리고 SaveDataMigrator에 변환을 추가한다.</summary>
        /// <remarks>v1 → v2(2026-11-08): aircraft 섹션(선택 기체) + 누적 기록 3개 추가.</remarks>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public SettingsData settings = new SettingsData();
        public RecordsData records = new RecordsData();
        public AircraftSaveData aircraft = new AircraftSaveData();

        public static SaveData CreateDefault() => new SaveData();
    }

    /// <summary>기체 선택 저장(애드온 명세 14.6). 해금은 기록으로 매번 계산하므로 저장하지 않는다(조작·불일치 방지).</summary>
    [Serializable]
    public sealed class AircraftSaveData
    {
        public string selectedAircraftId = TowerGlide.Core.Aircraft.AircraftIds.Default;

        public AircraftSaveData Sanitized() => new AircraftSaveData
        {
            selectedAircraftId = string.IsNullOrWhiteSpace(selectedAircraftId) || selectedAircraftId.Length > 64
                ? TowerGlide.Core.Aircraft.AircraftIds.Default
                : selectedAircraftId.Trim(),
        };
    }
}
