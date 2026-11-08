using System.Collections.Generic;
using System.Text;
using TowerGlide.Core.World;
using TowerGlide.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TowerGlide.World;
using UnityEngine;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// TOWERGLIDE ▸ 3. 코스 검증 — 열린 CityFlight 씬에서
    /// ① Core 규칙(간격·고도·선회각·상승각) ② 링 사이 직선 경로를 기체 크기(반지름 4.5m) 구로 훑어 막힌 구간 ③ 스폰·링 위치 겹침을 검사한다.
    /// 씬을 손으로 고친 뒤에도 실행해 "피할 공간이 없는 배치"(명세 2.5)를 막는다.
    /// </summary>
    public static class TowerglideCourseValidator
    {
        private const float ProbeRadius = 4.5f;

        [MenuItem("TOWERGLIDE/3. 코스 검증 (열린 CityFlight)", priority = 3)]
        public static void ValidateMenu() => ValidateOpenScene(showDialog: true);

        public static List<string> ValidateOpenScene(bool showDialog)
        {
            var issues = new List<string>();
            foreach (string s in CourseValidator.Validate(SkylineCourses.Downtown(), CourseRules.Default))
                issues.Add("[규칙] " + s);

            CheckpointCourse course = Object.FindFirstObjectByType<CheckpointCourse>();
            if (course == null)
            {
                issues.Add("[씬] CheckpointCourse가 없습니다 — CityFlight 씬을 열고 실행하세요.");
                Report(issues, showDialog);
                return issues;
            }

            // 청크 스트리밍(OW1): 건물·장애물은 청크 씬에 있으므로 검사 동안 모두 Additive로 연다
            var opened = new List<Scene>();
            var catalog = AssetDatabase.LoadAssetAtPath<WorldChunkCatalog>(TowerglideSceneBuilder.ChunkCatalogPath);
            if (catalog != null)
            {
                foreach (WorldChunkCatalog.Entry e in catalog.Entries)
                {
                    if (string.IsNullOrEmpty(e.scenePath) || SceneManager.GetSceneByPath(e.scenePath).isLoaded) continue;
                    opened.Add(EditorSceneManager.OpenScene(e.scenePath, OpenSceneMode.Additive));
                }
            }
            try
            {
                CheckGeometry(course, issues);
            }
            finally
            {
                foreach (Scene sc in opened) EditorSceneManager.CloseScene(sc, true);
            }

            Report(issues, showDialog);
            return issues;
        }

        private static void CheckGeometry(CheckpointCourse course, List<string> issues)
        {
            Physics.SyncTransforms();
            CheckpointGate[] gates = course.GetComponentsInChildren<CheckpointGate>(true);
            Transform spawn = GameObject.Find("SpawnPoint")?.transform;

            if (spawn != null && Overlaps(spawn.position, 6f, null))
                issues.Add("[스폰] 시작 지점이 장애물과 겹칩니다");

            Vector3 prev = spawn != null ? spawn.position : gates.Length > 0 ? gates[0].transform.position - gates[0].transform.forward * 60f : Vector3.zero;
            Transform prevGate = null;
            for (int i = 0; i < gates.Length; i++)
            {
                Transform g = gates[i].transform;
                Vector3 from = prevGate != null ? prev + prevGate.forward * 3f : prev;
                Vector3 to = g.position - g.forward * 3f;
                Vector3 dir = to - from;
                float dist = dir.magnitude;
                if (dist > 0.1f)
                {
                    RaycastHit[] hits = Physics.SphereCastAll(from, ProbeRadius, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);
                    foreach (RaycastHit hit in hits)
                    {
                        if (IsOwnRing(hit.collider, prevGate) || IsOwnRing(hit.collider, g)) continue;
                        if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // 시작부터 겹친 경우는 아래 겹침 검사로
                        issues.Add($"[경로] {(prevGate != null ? prevGate.name : "Spawn")} → {g.name}: '{Path(hit.collider.transform)}'에 막힘 ({hit.distance:0}m 지점)");
                        break;
                    }
                }
                if (Overlaps(g.position, TowerglidePrefabFactory.RingInnerRadius - 1f, g))
                    issues.Add($"[링] {g.name} 안쪽이 장애물과 겹칩니다");
                prev = g.position;
                prevGate = g;
            }
        }

        private static bool IsOwnRing(Collider c, Transform gate) => gate != null && c.transform.IsChildOf(gate);

        private static bool Overlaps(Vector3 pos, float radius, Transform ignore)
        {
            foreach (Collider c in Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignore != null && c.transform.IsChildOf(ignore)) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.GetComponent<Flight.AircraftFlightController>() != null) continue;
                return true;
            }
            return false;
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null && p.parent != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static void Report(List<string> issues, bool showDialog)
        {
            if (issues.Count == 0)
            {
                Debug.Log("[TOWERGLIDE] 코스 검증 통과: 규칙·경로·링·스폰 문제 없음");
                if (showDialog) EditorUtility.DisplayDialog("코스 검증", "문제 없음", "확인");
                return;
            }
            string text = string.Join("\n", issues);
            Debug.LogWarning($"[TOWERGLIDE] 코스 검증 문제 {issues.Count}건:\n{text}");
            if (showDialog) EditorUtility.DisplayDialog("코스 검증", $"문제 {issues.Count}건 — Console 참고\n\n{(text.Length > 600 ? text.Substring(0, 600) + "…" : text)}", "확인");
        }
    }
}
