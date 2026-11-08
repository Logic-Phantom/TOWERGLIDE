using System;
using UnityEngine;

namespace TowerGlide.Gameplay
{
    /// <summary>파티클이 끝나면 풀로 돌아간다(OnParticleSystemStopped ← stopAction Callback).</summary>
    public sealed class PooledParticle : MonoBehaviour
    {
        private Action<PooledParticle> _release;
        private bool _inUse;

        public ParticleSystem System { get; private set; }

        public void Init(ParticleSystem system, Action<PooledParticle> release)
        {
            System = system;
            _release = release;
        }

        private void OnEnable() => _inUse = true;

        private void OnParticleSystemStopped()
        {
            if (!_inUse) return;
            _inUse = false;
            _release?.Invoke(this);
        }
    }
}
