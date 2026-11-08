using System;

namespace TowerGlide.Core.Gameplay
{
    public enum CheckpointPassResult
    {
        /// <summary>다음 순서의 링을 정상 통과.</summary>
        Passed,
        /// <summary>마지막 링까지 통과해 코스 완주.</summary>
        Completed,
        /// <summary>이미 통과한 링 — 점수 중복 방지를 위해 무시.</summary>
        AlreadyPassed,
        /// <summary>순서를 건너뛴 링 — 무시(필수 링 순서 유지, 명세 3.3).</summary>
        OutOfOrder,
        /// <summary>이미 완주했거나 범위를 벗어난 인덱스.</summary>
        Ignored,
    }

    /// <summary>
    /// SKYLINE RUN 체크포인트 순서와 진행률. 링 통과 중복 판정 방지를 책임진다.
    /// </summary>
    public sealed class CheckpointSequence
    {
        public CheckpointSequence(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Course needs at least one checkpoint");
            Count = count;
        }

        public int Count { get; }
        /// <summary>다음에 통과해야 할 링 인덱스. 완주 시 Count.</summary>
        public int NextIndex { get; private set; }
        public int PassedCount => NextIndex;
        public bool IsComplete => NextIndex >= Count;
        public float Progress => (float)NextIndex / Count;
        /// <summary>마지막으로 통과한 링(복귀 지점). 아직 없으면 -1.</summary>
        public int LastPassedIndex => NextIndex - 1;

        public CheckpointPassResult TryPass(int index)
        {
            if (IsComplete || index < 0 || index >= Count) return CheckpointPassResult.Ignored;
            if (index < NextIndex) return CheckpointPassResult.AlreadyPassed;
            if (index > NextIndex) return CheckpointPassResult.OutOfOrder;

            NextIndex++;
            return IsComplete ? CheckpointPassResult.Completed : CheckpointPassResult.Passed;
        }

        public void Reset() => NextIndex = 0;
    }
}
