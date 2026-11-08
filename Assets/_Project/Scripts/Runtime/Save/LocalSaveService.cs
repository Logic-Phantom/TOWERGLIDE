using System;
using System.IO;
using TowerGlide.Core.Save;
using UnityEngine;

namespace TowerGlide.Save
{
    /// <summary>
    /// 설정·기록 로컬 저장(명세 4.3). 계정·클라우드 없음.
    /// - 위치: Application.persistentDataPath/save.json (앱 삭제 시 함께 삭제)
    /// - 원자적 쓰기: 임시 파일에 쓴 뒤 교체 → 쓰는 도중 앱이 죽어도 기존 파일 유지
    /// - 손상 파일은 save.corrupt-*.json으로 옮기고 기본값으로 계속 진행(게임 중단 없음)
    /// - 앱보다 새 버전의 저장 파일은 덮어쓰지 않는다(다운그레이드 보호)
    /// </summary>
    public sealed class LocalSaveService
    {
        private const string FileName = "save.json";
        private readonly string _directory;

        public LocalSaveService(string directory = null)
        {
            _directory = string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
        }

        public string FilePath => Path.Combine(_directory, FileName);
        public SaveData Data { get; private set; } = SaveData.CreateDefault();
        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.Missing;
        public bool IsReadOnly => LastLoadStatus == SaveLoadStatus.NewerVersion;

        public SaveData Load()
        {
            SaveData loaded = null;
            string path = FilePath;
            try
            {
                if (!File.Exists(path))
                {
                    Data = SaveData.CreateDefault();
                    LastLoadStatus = SaveLoadStatus.Missing;
                    return Data;
                }
                string json = File.ReadAllText(path);
                loaded = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 파일을 읽지 못했습니다. 기본값으로 진행합니다. ({e.GetType().Name})");
                loaded = null;
            }

            Data = SaveDataMigrator.Normalize(loaded, out SaveLoadStatus status);
            LastLoadStatus = status;
            if (status == SaveLoadStatus.Corrupted) BackupCorruptFile(path);
            return Data;
        }

        public bool Save()
        {
            if (IsReadOnly)
            {
                Debug.LogWarning("[Save] 더 새로운 버전의 저장 파일이 있어 덮어쓰지 않습니다.");
                return false;
            }

            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                Data.version = SaveData.CurrentVersion;
                File.WriteAllText(temp, JsonUtility.ToJson(Data, prettyPrint: false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 실패 — 게임은 계속 진행합니다. ({e.GetType().Name})");
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* ignore */ }
                return false;
            }
        }

        /// <summary>설정 → 기본값(기록은 유지).</summary>
        public void ResetSettings()
        {
            Data.settings = new SettingsData();
            Save();
        }

        private static void BackupCorruptFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string backup = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                    $"save.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                File.Move(path, backup);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 손상 파일 백업 실패 ({e.GetType().Name})");
            }
        }
    }
}
