using System;
using System.Collections.Generic;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;

namespace TowerGlide.Core.Aircraft
{
    public enum AircraftCategory
    {
        LightProp = 0,
        Aerobatic = 1,
        FighterJet = 2,
        RegionalAirliner = 3,
        Airliner = 4,
        WidebodyAirliner = 5,
        Cargo = 6,
    }

    /// <summary>기체가 어울리는 비행 공간(애드온 명세 14.7). 코스·활동 호환성 판단에 사용.</summary>
    [Flags]
    public enum AircraftTags
    {
        None = 0,
        TightRoute = 1 << 0,
        HighSpeed = 1 << 1,
        WideAirspace = 1 << 2,
        Aerobatics = 1 << 3,
    }

    public enum UnlockKind
    {
        Always = 0,
        TotalDistanceMeters = 1,
        TotalFlightSeconds = 2,
        SkylineCompletions = 3,
        /// <summary>미구현 기체 — 목록에 "개발 예정"으로만 표시, 선택 불가.</summary>
        ComingSoon = 4,
    }

    [Serializable]
    public struct UnlockRule
    {
        public UnlockKind Kind;
        public float Threshold;

        public static UnlockRule Always => new UnlockRule { Kind = UnlockKind.Always };
        public static UnlockRule ComingSoon => new UnlockRule { Kind = UnlockKind.ComingSoon };
    }

    /// <summary>기체 ID 상수(저장 데이터에 들어가므로 바꾸지 않는다).</summary>
    public static class AircraftIds
    {
        public const string ScoutP1 = "scout_p1";
        public const string ViperF9 = "viper_f9";
        public const string Cityliner180 = "cityliner_180";
        public const string AeroS2 = "aero_s2";
        public const string Metro70 = "metro_70";
        public const string Longhaul300 = "longhaul_300";
        public const string CargoLift220 = "cargolift_220";
        public const string Default = ScoutP1;
    }

    /// <summary>해금·선택·모드 호환성 규칙(순수 로직). 광고·결제·서버 없음 — 로컬 플레이 기록만 사용(애드온 명세 14.6).</summary>
    public static class AircraftRules
    {
        public static bool IsUnlocked(UnlockRule rule, RecordsData records)
        {
            records ??= new RecordsData();
            switch (rule.Kind)
            {
                case UnlockKind.Always: return true;
                case UnlockKind.TotalDistanceMeters: return records.totalDistanceMeters >= rule.Threshold;
                case UnlockKind.TotalFlightSeconds: return records.totalFlightSeconds >= rule.Threshold;
                case UnlockKind.SkylineCompletions: return records.skylineCompletions >= rule.Threshold;
                default: return false;
            }
        }

        /// <summary>해금 조건 안내 문구(UI용).</summary>
        public static string Describe(UnlockRule rule)
        {
            switch (rule.Kind)
            {
                case UnlockKind.Always: return "기본 제공";
                case UnlockKind.TotalDistanceMeters: return $"누적 비행 거리 {rule.Threshold / 1000f:0.#}km";
                case UnlockKind.TotalFlightSeconds: return $"누적 비행 시간 {rule.Threshold / 60f:0}분";
                case UnlockKind.SkylineCompletions: return $"SKYLINE RUN 완주 {rule.Threshold:0}회";
                default: return "개발 예정";
            }
        }

        /// <summary>
        /// 모드 호환성(14.7): SKYLINE RUN의 링(반지름 8m)은 소형기 전용(TightRoute). 여객기 등에 강제하지 않는다.
        /// </summary>
        public static bool CanPlay(FlightMode mode, AircraftTags tags, out string reason)
        {
            if (mode == FlightMode.SkylineRun && (tags & AircraftTags.TightRoute) == 0)
            {
                reason = "SKYLINE RUN의 링은 소형 기체 전용입니다. FREE FLIGHT로 비행하세요.";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// 저장된 ID를 실제 선택 가능한 기체로 해석한다. 없거나·잠겼거나·미구현이면 기본 기체(14.11 손상 저장 복구).
        /// </summary>
        public static string ResolveSelection(string savedId, IReadOnlyList<(string id, bool selectable)> catalog, string defaultId)
        {
            if (!string.IsNullOrEmpty(savedId))
                foreach ((string id, bool selectable) in catalog)
                    if (id == savedId && selectable) return id;
            foreach ((string id, bool selectable) in catalog)
                if (id == defaultId && selectable) return id;
            foreach ((string id, bool selectable) in catalog)
                if (selectable) return id;
            return defaultId;
        }
    }
}
