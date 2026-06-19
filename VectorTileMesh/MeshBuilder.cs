using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public static class MeshBuilder
    {
        public static void GenerateBottomMesh(
            List<List<Vector3>> rings,
            float height,
            ref List<Vector3> vertices,
            ref List<int> triangles,
            ref List<Vector2> uvs)
        {
            int ringCount = rings.Count;
            var subset = new List<List<Vector3>>(ringCount);
            int currentIndex = 0;

            for (int i = 0; i < ringCount; i++)
            {
                var sub = rings[i];
                if (sub == null || sub.Count == 0) continue;

                int vertCount = vertices.Count;

                if (IsClockwise(sub) && vertCount > 0)
                {
                    ProcessPolygonSubset(subset, ref currentIndex, vertices, triangles);
                    currentIndex = vertCount;
                    subset.Clear();
                }
                subset.Add(sub);

                AddPolygonVertices(sub, height, vertices, uvs);
            }

            ProcessPolygonSubset(subset, ref currentIndex, vertices, triangles);
        }

        public static void GenerateWallMesh(
            List<List<Vector3>> rings,
            float height,
            ref List<Vector3> vertices,
            ref List<int> triangles,
            ref List<Vector2> uvs,
            Span<ushort> holes)
        {
            if (height < 0.1f) return;

            int startIndex = vertices.Count;
            int ringCount = rings.Count;

            for (int i = 0; i < ringCount; i++)
            {
                var sub = rings[i];
                if (sub == null || sub.Count == 0) continue;

                int subCount = sub.Count;
                for (int j = 0; j < subCount; j++)
                {
                    int nextIndex = j % subCount;

                    if (IsHole(j, subCount - 1, holes) != IsHole(nextIndex, subCount - 1, holes))
                        continue;

                    var p = sub[j];
                    var pNext = sub[nextIndex];

                    AddWallQuad(p, pNext, height, ref startIndex, vertices, triangles, uvs);
                }
            }
        }

        public static void GenerateWallMesh(
            List<List<Vector3>> rings,
            float height,
            ref List<Vector3> vertices,
            ref List<int> triangles,
            ref List<Vector2> uvs)
        {
            if (height < 0.1f) return;

            int startIndex = vertices.Count;
            int ringCount = rings.Count;

            for (int i = 0; i < ringCount; i++)
            {
                var sub = rings[i];
                if (sub == null || sub.Count == 0) continue;

                int subCount = sub.Count;
                for (int j = 1; j < subCount; j++)
                {
                    bool isLast = j + 1 >= subCount;

                    var p = sub[j];
                    var pNext = sub[isLast ? 1 : j + 1];

                    AddWallQuad(p, pNext, height, ref startIndex, vertices, triangles, uvs);
                }
            }
        }

        public static void GenerateWallMesh(
            List<Vector3> rings,
            float height,
            ref List<Vector3> vertices,
            ref List<int> triangles,
            Span<ushort> holes)
        {
            if (height < 0.1f) return;

            int startIndex = vertices.Count;
            int ringCount = rings.Count;

            for (int i = 0; i < ringCount; i++)
            {
                int nextIndex = (i + 1) % ringCount;

                if (IsHole(i, ringCount - 1, holes) != IsHole(nextIndex, ringCount - 1, holes))
                    continue;

                var p = rings[i];
                var pNext = rings[nextIndex];

                vertices.Add(p - Vector3.up * height);
                vertices.Add(pNext - Vector3.up * height);
                vertices.Add(pNext);
                vertices.Add(p);

                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);

                triangles.Add(startIndex);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);

                startIndex += 4;
            }
        }

        public static List<int> GenerateWallMesh(List<Vector3> rings, float height, ref List<Vector3> vertices)
        {
            if (height < 0.1f) return null;

            int startIndex = vertices.Count;
            int ringCount = rings.Count;
            var wallTriangles = new List<int>(ringCount * 6);

            for (int i = 0; i < ringCount; i++)
            {
                int nextIndex = (i + 1) % ringCount;

                var p = rings[i];
                var pNext = rings[nextIndex];

                vertices.Add(p - Vector3.up * height);
                vertices.Add(pNext - Vector3.up * height);
                vertices.Add(pNext);
                vertices.Add(p);

                wallTriangles.Add(startIndex);
                wallTriangles.Add(startIndex + 1);
                wallTriangles.Add(startIndex + 2);

                wallTriangles.Add(startIndex);
                wallTriangles.Add(startIndex + 2);
                wallTriangles.Add(startIndex + 3);

                startIndex += 4;
            }

            return wallTriangles;
        }

        public static void GenerateWallMeshMergeTop(List<Vector3> rings, float height, ref List<Vector3> vertices, ref List<int> triangles)
        {
            if (height < 0.1f) return;

            int startIndex = vertices.Count;
            int ringCount = rings.Count;

            for (int i = 0; i < ringCount; i++)
            {
                var p = rings[i];
                vertices.Add(p - Vector3.up * height);
            }

            for (int i = 0; i < ringCount - 1; i++)
            {
                int nextIndex = i + 1;

                triangles.Add(i);
                triangles.Add(startIndex + i);
                triangles.Add(startIndex + nextIndex);

                triangles.Add(i);
                triangles.Add(startIndex + nextIndex);
                triangles.Add(nextIndex);
            }

            triangles.Add(startIndex - 1);
            triangles.Add(vertices.Count - 1);
            triangles.Add(startIndex);

            triangles.Add(startIndex - 1);
            triangles.Add(startIndex);
            triangles.Add(0);
        }

        private static void ProcessPolygonSubset(
            List<List<Vector3>> subset,
            ref int currentIndex,
            List<Vector3> vertices,
            List<int> triangles)
        {
            var flatData = EarcutLibrarySDK.Flatten(subset);
            if (flatData.Vertices.Count == 0) return;

            var result = EarcutLibrarySDK.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim);
            int polygonVertexCount = result.Count;

            for (int j = 0; j < polygonVertexCount; j++)
            {
                triangles.Add(result[j] + currentIndex);
            }
        }

        private static void AddPolygonVertices(
            List<Vector3> sub,
            float height,
            List<Vector3> vertices,
            List<Vector2> uvs)
        {
            int count = sub.Count;
            vertices.Capacity = vertices.Count + count;

            for (int j = 0; j < count; j++)
            {
                var vertex = sub[j] + Vector3.up * height;
                vertices.Add(vertex);
                uvs.Add(CalculateUV(vertex));
            }
        }

        private static void AddWallQuad(
            Vector3 p,
            Vector3 pNext,
            float height,
            ref int startIndex,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uvs)
        {
            vertices.Add(p - Vector3.up * height);
            uvs.Add(new Vector2(1, 0));
            
            vertices.Add(pNext - Vector3.up * height);
            uvs.Add(new Vector2(0, 0));
            
            vertices.Add(pNext);
            uvs.Add(new Vector2(0, 1));
            
            vertices.Add(p);
            uvs.Add(new Vector2(1, 1));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);

            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);

            startIndex += 4;
        }

        public static void GenerateRoadMesh(
            List<Vector3> road,
            float halfWidth,
            ref List<Vector3> vertices,
            ref List<Vector2> uvs,
            ref List<int> triangles)
        {
            GenerateSingleRoad(road, halfWidth, Vector3.up, vertices, uvs, triangles);
        }

        public static void GenerateRoadMesh(
            List<List<Vector3>> roads,
            float halfWidth,
            ref List<Vector3> vertices,
            ref List<Vector2> uvs,
            ref List<int> triangles)
        {
            Vector3 up = Vector3.up;

            foreach (var road in roads)
            {
                GenerateSingleRoad(road, halfWidth, up, vertices, uvs, triangles);
            }
        }

        private static void GenerateSingleRoad(
            List<Vector3> road,
            float halfWidth,
            Vector3 up,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int pointCount = road.Count;
            if (pointCount < 2) return;

            var lengths = new List<float>(pointCount) { 0.0f };
            var inners = new List<Vector3>(pointCount - 1);
            var outers = new List<Vector3>(pointCount - 1);

            float sumLen = 0.0f;

            for (int i = 0; i < pointCount - 1; i++)
            {
                Vector3 dir = (road[i + 1] - road[i]).normalized;
                inners.Add(Vector3.Cross(up, dir));
                outers.Add(Vector3.Cross(dir, up));
                sumLen += Vector3.Distance(road[i], road[i + 1]);
                lengths.Add(sumLen);
            }

            for (int i = 0; i < pointCount - 1; i++)
            {
                int startIndex = vertices.Count;

                float curY = lengths[i];
                float nextY = lengths[i + 1];

                vertices.Add(road[i] + inners[i] * halfWidth);
                uvs.Add(new Vector2(0.0f, curY));

                vertices.Add(road[i] + outers[i] * halfWidth);
                uvs.Add(new Vector2(1.0f, curY));

                vertices.Add(road[i + 1] + inners[i] * halfWidth);
                uvs.Add(new Vector2(0.0f, nextY));

                vertices.Add(road[i + 1] + outers[i] * halfWidth);
                uvs.Add(new Vector2(1.0f, nextY));

                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);

                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 3);
                triangles.Add(startIndex + 2);
            }
        }

        public static void GenerateArrowMesh(
            List<Vector3> road,
            float extensionLength,
            float angle,
            ref List<Vector3> vertices,
            ref List<Vector2> uvs,
            ref List<int> triangles)
        {
            Vector3 lastPoint = road[road.Count - 1];
            Vector3 lastDir = (road[road.Count - 1] - road[road.Count - 2]).normalized;

            Vector3 leftDir = Quaternion.Euler(0, -angle, 0) * lastDir;
            Vector3 rightDir = Quaternion.Euler(0, angle, 0) * lastDir;

            Vector3 extensionLeft = lastPoint - leftDir * extensionLength;
            Vector3 extensionRight = lastPoint - rightDir * extensionLength;

            int startIndex = vertices.Count;
            vertices.Add(extensionLeft);
            uvs.Add(new Vector2(0.0f, 0.0f));

            vertices.Add(extensionRight);
            uvs.Add(new Vector2(1.0f, 0.0f));

            vertices.Add(lastPoint);
            uvs.Add(new Vector2(0.5f, 1.0f));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
        }

        public static void GenerateVArrowMesh(
            List<Vector3> road,
            float halfWidth,
            float angle,
            float extensionLength,
            ref List<Vector3> vertices,
            ref List<Vector2> uvs,
            ref List<int> triangles)
        {
            Vector3 lastPoint = road[road.Count - 1];
            Vector3 lastDir = (road[road.Count - 1] - road[road.Count - 2]).normalized;

            Vector3 leftDir = Quaternion.Euler(0, -angle, 0) * lastDir;
            Vector3 rightDir = Quaternion.Euler(0, angle, 0) * lastDir;

            Vector3 extensionLeft = lastPoint - leftDir * extensionLength;
            Vector3 extensionRight = lastPoint - rightDir * extensionLength;

            var arrowValue = new List<List<Vector3>>(3)
            {
                road,
                new List<Vector3> { lastPoint, extensionLeft },
                new List<Vector3> { lastPoint, extensionRight }
            };

            GenerateRoadMesh(arrowValue, halfWidth, ref vertices, ref uvs, ref triangles);
        }

        public static List<Vector3> ExtrudeAlongPath(List<Vector3> path, float width)
        {
            if (path.Count < 2) return path;

            int pointCount = path.Count - 1;
            var right = new List<Vector3>(pointCount);

            for (int i = 0; i < pointCount; i++)
            {
                var dir = (path[i + 1] - path[i]).normalized;
                right.Add(Vector3.Cross(Vector3.up, dir).normalized);
            }

            var result = new List<Vector3>(path.Count * 2 + 1);

            for (int i = 0; i < pointCount; i++)
            {
                result.Add(path[i] + right[i] * width);
            }

            var lastRight = right[pointCount - 1];
            result.Add(path[pointCount] + lastRight * width);

            for (int i = pointCount; i >= 0; i--)
            {
                var r = i < pointCount ? right[i] : lastRight;
                result.Add(path[System.Math.Min(i, pointCount)] - r * width);
            }

            result.Add(result[0]);

            return result;
        }

        public static List<Vector3> ExtrudeAlongPath(List<Vector3> path, float width, ref List<Vector2> uvs)
        {
            if (path.Count < 2) return path;

            int pointCount = path.Count - 1;
            var right = new List<Vector3>(pointCount);

            float totalLength = 0f;
            for (int i = 0; i < pointCount; i++)
            {
                totalLength += Vector3.Distance(path[i], path[i + 1]);
            }

            float accumulatedLength = 0f;

            for (int i = 0; i < pointCount; i++)
            {
                var dir = (path[i + 1] - path[i]).normalized;
                right.Add(Vector3.Cross(Vector3.up, dir).normalized);
            }

            var result = new List<Vector3>(path.Count * 2 + 1);

            for (int i = 0; i < pointCount; i++)
            {
                result.Add(path[i] + right[i] * width);
                float v = accumulatedLength / totalLength;
                uvs.Add(new Vector2(0, v));
                uvs.Add(new Vector2(1, v));
                accumulatedLength += Vector3.Distance(path[i], path[i + 1]);
            }

            var lastRight = right[pointCount - 1];
            result.Add(path[pointCount] + lastRight * width);
            float lastV = accumulatedLength / totalLength;
            uvs.Add(new Vector2(0, lastV));
            uvs.Add(new Vector2(1, lastV));

            for (int i = pointCount; i >= 0; i--)
            {
                var r = i < pointCount ? right[i] : lastRight;
                result.Add(path[System.Math.Min(i, pointCount)] - r * width);
            }

            result.Add(result[0]);
            uvs.Add(uvs[0]);

            return result;
        }

        public static List<Vector3> ExtrudeAlongPath(List<Vector3> path, float width, float height)
        {
            if (path.Count < 2) return path;

            int pointCount = path.Count - 1;
            var right = new List<Vector3>(pointCount);

            for (int i = 0; i < pointCount; i++)
            {
                var dir = (path[i + 1] - path[i]).normalized;
                right.Add(Vector3.Cross(Vector3.up, dir).normalized);
            }

            var result = new List<Vector3>(path.Count * 2 + 1);

            for (int i = 0; i < pointCount; i++)
            {
                result.Add(path[i] + Vector3.up * height + right[i] * width);
            }

            var lastRight = right[pointCount - 1];
            result.Add(path[pointCount] + Vector3.up * height + lastRight * width);

            for (int i = pointCount; i >= 0; i--)
            {
                var r = i < pointCount ? right[i] : lastRight;
                result.Add(path[System.Math.Min(i, pointCount)] + Vector3.up * height - r * width);
            }

            result.Add(result[0]);

            return result;
        }

        public static List<int> ExtrudeAlongPathTriangles(List<Vector3> rings)
        {
            if (rings.Count < 1) return null;

            int vertexCount = rings.Count;
            int triangleCount = vertexCount - 2;

            var triangles = new List<int>(triangleCount * 3 + 3);

            for (int j = 0; j < triangleCount; j++)
            {
                triangles.Add(j);
                triangles.Add(j + 1);
                triangles.Add(vertexCount - 2 - j);
            }

            if (triangleCount > 1)
            {
                triangles.Add(1);
                triangles.Add(vertexCount - 3);
                triangles.Add(vertexCount - 2);
            }

            return triangles;
        }

        public static List<Vector2> GenerateUVs(List<Vector3> vertices)
        {
            var uvs = new List<Vector2>(vertices.Count);

            float minX = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxZ = float.MinValue;

            int count = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                var v = vertices[i];
                if (v.x < minX) minX = v.x;
                if (v.z < minZ) minZ = v.z;
                if (v.x > maxX) maxX = v.x;
                if (v.z > maxZ) maxZ = v.z;
            }

            float subX = maxX - minX;
            float subZ = maxZ - minZ;

            for (int i = 0; i < count; i++)
            {
                var v = vertices[i];
                uvs.Add(new Vector2((v.x - minX) / subX, (v.z - minZ) / subZ));
            }

            return uvs;
        }

        public static (List<Vector3>, List<int>, List<Vector2>) GenerateVectorMesh(
            List<List<Vector3>> worldRings,
            float height,
            bool isWall,
            Span<ushort> holes)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            GenerateBottomMesh(worldRings, isWall ? height : 0, ref vertices, ref triangles, ref uvs);
            
            if (isWall)
            {
                GenerateWallMesh(worldRings, height, ref vertices, ref triangles, ref uvs, holes);
            }

            return (vertices, triangles, uvs);
        }

        public static Mesh GenerateVectorMesh(List<List<Vector3>> worldRings, float height, bool isWall)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            GenerateBottomMesh(worldRings, isWall ? height : 0, ref vertices, ref triangles, ref uvs);
            
            if (isWall)
            {
                GenerateWallMesh(worldRings, height, ref vertices, ref triangles, ref uvs);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }

        public static List<int> CombieTriangle(List<int> vertexCounts, List<List<int>> triangles)
        {
            var result = new List<int>();
            int start = 0;

            for (int i = 0; i < vertexCounts.Count; i++)
            {
                var tri = triangles[i];
                int count = tri.Count;
                for (int j = 0; j < count; j++)
                {
                    result.Add(tri[j] + start);
                }
                start += vertexCounts[i];
            }

            return result;
        }

        public static Vector2 GetUVs(Vector3 vertex)
        {
            float worldScale = 6367f;
            Vector2 uv = new Vector2(vertex.x / worldScale, vertex.z / worldScale);
            return uv;
        }

        private static bool IsClockwise(IList<Vector3> vertices)
        {
            double sum = 0.0;
            int count = vertices.Count;

            for (int i = 0; i < count; i++)
            {
                Vector3 v1 = vertices[i];
                Vector3 v2 = vertices[(i + 1) % count];
                sum += (v2.x - v1.x) * (v2.z + v1.z);
            }

            return sum > 0.0;
        }

        public static bool IsHole(int index, int maxIndex, Span<ushort> holes)
        {
            if (holes.Length == 0) return false;

            for (int i = 0; i < holes.Length; i += 2)
            {
                int holeStartIndex = holes[i];
                int holeEndIndex = i + 1 < holes.Length ? holes[i + 1] - 1 : maxIndex;
                if (index >= holeStartIndex && index <= holeEndIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector2 CalculateUV(Vector3 vertex)
        {
            float worldScale = 100f;
            return new Vector2(vertex.x / worldScale, vertex.z / worldScale);
        }
    }
}