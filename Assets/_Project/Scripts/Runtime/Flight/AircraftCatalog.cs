using System;
using System.Collections.Generic;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.Save;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>
    /// 선택 가능한 기체 목록 + 선택 해석(애드온 명세 14.4, 14.6). 씬들이 같은 에셋을 참조한다.
    /// 잠긴/미구현 기체는 목록에 보이되 선택되지 않는다. 저장된 ID가 이상하면 기본 기체로 복구.
    /// </summary>
    [CreateAssetMenu(menuName = "TOWERGLIDE/Aircraft Catalog", fileName = "AircraftCatalog")]
    public sealed class AircraftCatalog : ScriptableObject
    {
        [SerializeField] private AircraftDefinition[] aircraft = Array.Empty<AircraftDefinition>();
        [SerializeField] private string defaultId = AircraftIds.Default;

        public IReadOnlyList<AircraftDefinition> All => aircraft;
        public string DefaultId => defaultId;

        public AircraftDefinition Find(string id)
        {
            foreach (AircraftDefinition d in aircraft)
                if (d != null && d.id == id) return d;
            return null;
        }

        public bool IsSelectable(AircraftDefinition d, RecordsData records) =>
            d != null && d.IsImplemented && AircraftRules.IsUnlocked(d.unlock, records);

        /// <summary>저장된 선택값 → 실제 사용할 기체(없으면 기본).</summary>
        public AircraftDefinition Resolve(string savedId, RecordsData records)
        {
            var entries = new List<(string id, bool selectable)>(aircraft.Length);
            foreach (AircraftDefinition d in aircraft)
                if (d != null) entries.Add((d.id, IsSelectable(d, records)));
            return Find(AircraftRules.ResolveSelection(savedId, entries, defaultId));
        }

        /// <summary>구현된 기체끼리의 상대 성능 막대(순서 = All 중 구현된 것).</summary>
        public Dictionary<string, AircraftStatBars> ComputeStatBars()
        {
            var implemented = new List<AircraftDefinition>();
            foreach (AircraftDefinition d in aircraft)
                if (d != null && d.IsImplemented) implemented.Add(d);
            var numbers = new FlightProfileNumbers[implemented.Count];
            for (int i = 0; i < implemented.Count; i++) numbers[i] = implemented[i].ProfileNumbers;
            AircraftStatBars[] bars = AircraftStats.Compute(numbers);
            var result = new Dictionary<string, AircraftStatBars>();
            for (int i = 0; i < implemented.Count; i++) result[implemented[i].id] = bars[i];
            return result;
        }

        /// <summary>에디터 생성기 전용.</summary>
        public void SetEntries(AircraftDefinition[] entries, string defaultAircraftId)
        {
            aircraft = entries;
            defaultId = defaultAircraftId;
        }
    }
}
