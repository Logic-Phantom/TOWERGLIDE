using System.Collections.Generic;
using UnityEngine;

namespace TowerGlide.World
{
    /// <summary>
    /// 미터 단위 UV를 가진 박스 메시. 건물 외벽에 창문 텍스처가 크기와 상관없이 같은 간격(층고)으로 반복되게 한다(명세 3.2 "창문은 반복 텍스처").
    /// 서브메시 0 = 옆면(외벽 머티리얼), 1 = 윗면·아랫면(지붕 머티리얼).
    /// </summary>
    public static class ProceduralMeshes
    {
        /// <param name="size">가로(X)·높이(Y)·깊이(Z) m</param>
        /// <param name="tileWidth">창문 한 칸 가로(m)</param>
        /// <param name="tileHeight">층고(m)</param>
        /// <param name="pivotAtBottom">true면 원점이 바닥 중앙</param>
        public static Mesh CreateMetricBox(Vector3 size, float tileWidth = 4f, float tileHeight = 3.6f, bool pivotAtBottom = true)
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var sides = new List<int>(24);
            var caps = new List<int>(12);

            Vector3 h = size * 0.5f;
            Vector3 offset = pivotAtBottom ? new Vector3(0f, h.y, 0f) : Vector3.zero;

            // 옆면: u = 가로, v = 위. Cross(u, v) = n 이 되도록 u = Cross(up, n)
            AddFace(new Vector3(h.x, 0f, 0f), Vector3.right, h.z, h.y, true);
            AddFace(new Vector3(-h.x, 0f, 0f), Vector3.left, h.z, h.y, true);
            AddFace(new Vector3(0f, 0f, h.z), Vector3.forward, h.x, h.y, true);
            AddFace(new Vector3(0f, 0f, -h.z), Vector3.back, h.x, h.y, true);
            AddFace(new Vector3(0f, h.y, 0f), Vector3.up, h.x, h.z, false);
            AddFace(new Vector3(0f, -h.y, 0f), Vector3.down, h.x, h.z, false);

            var mesh = new Mesh { name = $"MetricBox_{size.x:0}x{size.y:0}x{size.z:0}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sides, 0);
            mesh.SetTriangles(caps, 1);
            mesh.RecalculateBounds();
            return mesh;

            void AddFace(Vector3 center, Vector3 n, float halfU, float halfV, bool side)
            {
                Vector3 u, v;
                if (side)
                {
                    u = Vector3.Cross(Vector3.up, n);
                    v = Vector3.up;
                }
                else
                {
                    u = Vector3.right;
                    v = Vector3.Cross(n, u);
                }

                int start = vertices.Count;
                Vector3 c = center + offset;
                Vector3[] corners =
                {
                    c - u * halfU - v * halfV,
                    c + u * halfU - v * halfV,
                    c + u * halfU + v * halfV,
                    c - u * halfU + v * halfV,
                };
                foreach (Vector3 p in corners)
                {
                    vertices.Add(p);
                    normals.Add(n);
                    if (side)
                    {
                        // 가로: 면 위 좌표(0 ~ 2*halfU), 세로: 바닥에서의 높이 → 층·창문이 모든 건물에서 정렬됨
                        float along = Vector3.Dot(p - offset, u) + halfU;
                        float up = p.y - (offset.y - h.y);
                        uvs.Add(new Vector2(along / tileWidth, up / tileHeight));
                    }
                    else
                    {
                        uvs.Add(new Vector2(p.x / 10f, p.z / 10f));
                    }
                }

                List<int> tris = side ? sides : caps;
                // 왼손 좌표계 앞면 = 시계 방향(바깥에서 볼 때). Cross(u, v) = n 이면 (0,1,2), (0,2,3)
                tris.Add(start);
                tris.Add(start + 1);
                tris.Add(start + 2);
                tris.Add(start);
                tris.Add(start + 2);
                tris.Add(start + 3);
            }
        }
    }
}
