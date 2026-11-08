using TowerGlide.Core.Save;
using TowerGlide.Save;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>
    /// 현재 선택 기체 상태(애드온 명세 14.6). GameApp이 소유. 카탈로그는 Resources/AircraftCatalog(에디터 생성).
    /// 선택은 비행 시작 전에만 바꾼다(공중 교체 없음).
    /// </summary>
    public sealed class AircraftSelectionService
    {
        public const string CatalogResourcePath = "AircraftCatalog";

        private readonly LocalSaveService _save;

        public AircraftSelectionService(LocalSaveService save, AircraftCatalog catalog)
        {
            _save = save;
            Catalog = catalog;
            if (catalog == null)
                Debug.LogWarning($"[Aircraft] Resources/{CatalogResourcePath} 없음 — TOWERGLIDE ▸ 2. 씬 생성을 실행하세요. 씬에 놓인 기체로 비행합니다.");
        }

        public AircraftCatalog Catalog { get; }

        public AircraftDefinition Selected =>
            Catalog != null ? Catalog.Resolve(_save.Data.aircraft.selectedAircraftId, _save.Data.records) : null;

        /// <returns>선택 가능해서 저장했으면 true</returns>
        public bool Select(AircraftDefinition definition)
        {
            if (Catalog == null || !Catalog.IsSelectable(definition, _save.Data.records)) return false;
            _save.Data.aircraft ??= new AircraftSaveData();
            _save.Data.aircraft.selectedAircraftId = definition.id;
            _save.Save();
            return true;
        }
    }
}
