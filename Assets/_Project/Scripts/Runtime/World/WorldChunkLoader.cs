using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TowerGlide.Core.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace TowerGlide.World
{
    /// <summary>
    /// 청크 씬 비동기 로드/언로드 실행기(애드온 명세 5.3: SceneManager.LoadSceneAsync Additive / UnloadSceneAsync).
    /// 상태는 Core ChunkStateTracker가 관리 → 같은 청크 중복 요청을 막는다. 로드 시간은 측정해 디버그 HUD·문서에 쓴다.
    /// </summary>
    public sealed class WorldChunkLoader : MonoBehaviour
    {
        [SerializeField] private WorldChunkCatalog catalog;

        private readonly ChunkStateTracker _tracker = new ChunkStateTracker();
        private readonly Dictionary<WorldChunkId, Scene> _scenes = new Dictionary<WorldChunkId, Scene>();

        public ChunkStateTracker Tracker => _tracker;
        public WorldChunkCatalog Catalog => catalog;
        /// <summary>최근 로드 시간(ms) — 디버그용.</summary>
        public float LastLoadMilliseconds { get; private set; }
        public float MaxLoadMilliseconds { get; private set; }

        public event Action<WorldChunkId> ChunkLoaded;
        public event Action<WorldChunkId> ChunkUnloaded;
        public event Action<WorldChunkId> ChunkFailed;

        public void SetCatalog(WorldChunkCatalog value) => catalog = value;

        public bool RequestLoad(WorldChunkId id)
        {
            if (!_tracker.BeginLoad(id)) return false;
            if (catalog == null || !catalog.TryGetScene(id, out string path))
            {
                // 내용 없는 청크: 즉시 로드 완료(빈 공간도 "준비됨")
                _tracker.CompleteLoad(id);
                ChunkLoaded?.Invoke(id);
                return true;
            }
            StartCoroutine(LoadRoutine(id, path));
            return true;
        }

        public bool RequestUnload(WorldChunkId id)
        {
            if (!_tracker.BeginUnload(id)) return false;
            if (!_scenes.TryGetValue(id, out Scene scene) || !scene.IsValid() || !scene.isLoaded)
            {
                _scenes.Remove(id);
                _tracker.CompleteUnload(id);
                ChunkUnloaded?.Invoke(id);
                return true;
            }
            StartCoroutine(UnloadRoutine(id, scene));
            return true;
        }

        private IEnumerator LoadRoutine(WorldChunkId id, string path)
        {
            var sw = Stopwatch.StartNew();
            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Streaming] {id} 로드 시작 실패: {e.GetType().Name}");
            }

            if (op == null)
            {
                // Build Settings에 없거나 경로 오류 → Failed(재시도 대기). 원거리 실루엣은 계속 보인다(fallback)
                _tracker.FailLoad(id, Time.time);
                ChunkFailed?.Invoke(id);
                yield break;
            }
            op.priority = 0; // 진행 중 프레임 영향 최소
            while (!op.isDone) yield return null;

            Scene scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                _tracker.FailLoad(id, Time.time);
                ChunkFailed?.Invoke(id);
                yield break;
            }

            _scenes[id] = scene;
            sw.Stop();
            LastLoadMilliseconds = (float)sw.Elapsed.TotalMilliseconds;
            MaxLoadMilliseconds = Mathf.Max(MaxLoadMilliseconds, LastLoadMilliseconds);

            if (_tracker.Get(id) == WorldChunkState.Loading)
            {
                _tracker.CompleteLoad(id);
                ChunkLoaded?.Invoke(id);
            }
        }

        private IEnumerator UnloadRoutine(WorldChunkId id, Scene scene)
        {
            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            if (op != null)
                while (!op.isDone) yield return null;
            _scenes.Remove(id);
            _tracker.CompleteUnload(id);
            ChunkUnloaded?.Invoke(id);
        }

        /// <summary>비행 씬을 떠날 때 남은 청크 씬 정리(단일 모드 씬 전환이면 자동으로 내려가지만 명시적으로).</summary>
        private void OnDestroy()
        {
            ChunkLoaded = null;
            ChunkUnloaded = null;
            ChunkFailed = null;
        }
    }
}
