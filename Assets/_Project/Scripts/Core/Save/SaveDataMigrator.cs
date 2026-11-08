namespace TowerGlide.Core.Save
{
    public enum SaveLoadStatus
    {
        Ok,
        /// <summary>파일 없음(첫 실행) → 기본값.</summary>
        Missing,
        /// <summary>파싱 실패·null → 기본값으로 복구(원본은 Runtime이 백업).</summary>
        Corrupted,
        /// <summary>이전 버전에서 변환됨.</summary>
        Migrated,
        /// <summary>앱보다 새로운 버전의 저장 파일(다운그레이드) → 읽을 수 있는 값만 쓰고 덮어쓰지 않는다.</summary>
        NewerVersion,
    }

    /// <summary>
    /// 역직렬화 직후의 SaveData를 검증·이관한다. 어떤 입력이 와도 사용 가능한 SaveData를 돌려준다(명세 4.3: 저장 실패가 게임을 멈추지 않음).
    /// JSON 파싱 자체는 Runtime(JsonUtility)이 담당한다.
    /// </summary>
    public static class SaveDataMigrator
    {
        public static SaveData Normalize(SaveData loaded, out SaveLoadStatus status)
        {
            if (loaded == null)
            {
                status = SaveLoadStatus.Corrupted;
                return SaveData.CreateDefault();
            }

            status = SaveLoadStatus.Ok;
            if (loaded.version <= 0)
            {
                // version 필드가 없던(0으로 읽힌) 초안 데이터 → 현재 형식으로 간주하고 값만 검증
                status = SaveLoadStatus.Migrated;
            }
            else if (loaded.version > SaveData.CurrentVersion)
            {
                status = SaveLoadStatus.NewerVersion;
            }
            else if (loaded.version < SaveData.CurrentVersion)
            {
                // v1 → v2: aircraft 섹션·누적 기록 추가. JsonUtility는 없는 필드를 기본값으로 두므로 값만 채운다.
                // 누적 거리·시간은 v1에 없었다 → 0에서 시작(기존 최장 거리로 추정하지 않음: 해금 조건을 부풀리지 않기 위해).
                status = SaveLoadStatus.Migrated;
            }

            return new SaveData
            {
                version = status == SaveLoadStatus.NewerVersion ? loaded.version : SaveData.CurrentVersion,
                settings = (loaded.settings ?? new SettingsData()).Sanitized(),
                records = (loaded.records ?? new RecordsData()).Sanitized(),
                aircraft = (loaded.aircraft ?? new AircraftSaveData()).Sanitized(),
            };
        }
    }
}
