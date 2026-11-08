using System;
using UnityEngine;
using UnityEngine.Pool;

namespace TowerGlide.Gameplay
{
    /// <summary>
    /// 충돌·링 통과·근접 회피 시각 피드백(명세 2.4, 4.2). 짧고 적은 파티클 + 오브젝트 풀(명세 6.2, 10.2) — 런 중 Instantiate/Destroy 없음.
    /// 파티클 프리팹은 TOWERGLIDE ▸ 2. 씬 생성이 Prefabs/VFX에 만든다(stopAction = Callback).
    /// </summary>
    public sealed class FeedbackFx : MonoBehaviour
    {
        [SerializeField] private ParticleSystem crashPrefab;
        [SerializeField] private ParticleSystem ringPassPrefab;
        [SerializeField] private ParticleSystem nearMissPrefab;
        [SerializeField] private int prewarmPerType = 3;
        [SerializeField] private int maxPerType = 8;

        private ObjectPool<PooledParticle> _crash;
        private ObjectPool<PooledParticle> _ring;
        private ObjectPool<PooledParticle> _nearMiss;

        private void Awake()
        {
            _crash = CreatePool(crashPrefab, "Crash");
            _ring = CreatePool(ringPassPrefab, "RingPass");
            _nearMiss = CreatePool(nearMissPrefab, "NearMiss");
        }

        public void PlayCrash(Vector3 position, Vector3 normal) => Play(_crash, position, normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.identity);

        public void PlayRingPass(Vector3 position, Quaternion rotation) => Play(_ring, position, rotation);

        public void PlayNearMiss(Vector3 position, Quaternion rotation) => Play(_nearMiss, position, rotation);

        private static void Play(ObjectPool<PooledParticle> pool, Vector3 position, Quaternion rotation)
        {
            if (pool == null) return;
            PooledParticle p = pool.Get();
            p.transform.SetPositionAndRotation(position, rotation);
            p.System.Play(true);
        }

        private ObjectPool<PooledParticle> CreatePool(ParticleSystem prefab, string label)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(FeedbackFx)}] {label} 파티클 프리팹 미연결 — 해당 효과 생략", this);
                return null;
            }

            ObjectPool<PooledParticle> pool = null;
            pool = new ObjectPool<PooledParticle>(
                createFunc: () =>
                {
                    ParticleSystem ps = Instantiate(prefab, transform);
                    ps.gameObject.name = $"{label}_Pooled";
                    ParticleSystem.MainModule main = ps.main;
                    main.stopAction = ParticleSystemStopAction.Callback;
                    main.playOnAwake = false;
                    PooledParticle pp = ps.gameObject.AddComponent<PooledParticle>();
                    pp.Init(ps, x => pool.Release(x));
                    return pp;
                },
                actionOnGet: x => x.gameObject.SetActive(true),
                actionOnRelease: x => x.gameObject.SetActive(false),
                actionOnDestroy: x => { if (x != null) Destroy(x.gameObject); },
                collectionCheck: false,
                defaultCapacity: prewarmPerType,
                maxSize: maxPerType);

            // 미리 만들어 두기(첫 충돌 때 프레임 스파이크 방지)
            var warm = new PooledParticle[prewarmPerType];
            for (int i = 0; i < warm.Length; i++) warm[i] = pool.Get();
            foreach (PooledParticle w in warm) pool.Release(w);
            return pool;
        }
    }
}
