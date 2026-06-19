using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeoTiles
{
    public static class OBJExporter
    {
        public static void ExportToOBJ(string objPath, List<Vector3> vertices, List<Vector3> colors, List<Vector2> uvs, List<int> triangles)
        {
            try
            {
                if (vertices == null || vertices.Count == 0 || triangles == null || triangles.Count % 3 != 0)
                {
                    Debug.LogError("Invalid mesh data for OBJ export.");
                    return;
                }

                bool hasColors = colors != null && colors.Count == vertices.Count;
                bool hasUVs = uvs != null && uvs.Count == vertices.Count;

                Directory.CreateDirectory(Path.GetDirectoryName(objPath) ?? ".");

                using StreamWriter sw = new StreamWriter(objPath);
                sw.WriteLine("# Exported by Unity");
                sw.WriteLine($"# Vertex count: {vertices.Count}");
                sw.WriteLine($"# Face count: {triangles.Count / 3}");
                sw.WriteLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                // 写入顶点
                for (int i = 0; i < vertices.Count; i++)
                {
                    var v = vertices[i];
                    if (hasColors)
                    {
                        var c = colors[i];
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2} {3} {4} {5}", v.x, v.y, v.z,
                            Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z)));
                    }
                    else
                    {
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", v.x, v.y, v.z));
                    }
                }

                // 写入UV
                if (hasUVs)
                {
                    foreach (var uv in uvs)
                    {
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "vt {0} {1}", uv.x, uv.y));
                    }
                }

                // 写入面
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int idx0 = triangles[i] + 1;
                    int idx1 = triangles[i + 1] + 1;
                    int idx2 = triangles[i + 2] + 1;

                    if (hasUVs)
                    {
                        sw.WriteLine($"f {idx0}/{idx0} {idx1}/{idx1} {idx2}/{idx2}");
                    }
                    else
                    {
                        sw.WriteLine($"f {idx0} {idx1} {idx2}");
                    }
                }

                Debug.Log($"OBJ saved to: {objPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ExportToOBJ failed: {ex.Message}");
            }
        }

        public static void ImportFromOBJ(string objPath, out List<Vector3> vertices, out List<Vector2> uvs, out List<int> triangles)
        {
            vertices = new List<Vector3>();
            uvs = new List<Vector2>();
            triangles = new List<int>();

            try
            {
                if (!File.Exists(objPath))
                {
                    Debug.LogError("OBJ file not found: " + objPath);
                    return;
                }

                List<Vector3> tempVerts = new();
                List<Vector2> tempUVs = new();

                foreach (string line in File.ReadLines(objPath))
                {
                    if (line.StartsWith("v ", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                            float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                            float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
                            tempVerts.Add(new Vector3(x, y, z));
                        }
                    }
                    else if (line.StartsWith("vt ", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            float u = float.Parse(parts[1], CultureInfo.InvariantCulture);
                            float v = float.Parse(parts[2], CultureInfo.InvariantCulture);
                            tempUVs.Add(new Vector2(u, v));
                        }
                    }
                    else if (line.StartsWith("f ", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 4)
                        {
                            for (int i = 1; i <= 3; i++)
                            {
                                var vertexInfo = parts[i].Split('/');
                                if (vertexInfo.Length > 0)
                                {
                                    int vIdx = int.Parse(vertexInfo[0]) - 1;
                                    triangles.Add(vIdx);
                                }
                            }
                        }
                    }
                }

                vertices = tempVerts;
                uvs = tempUVs.Count == tempVerts.Count ? tempUVs : new List<Vector2>();
                Debug.Log($"OBJ imported from: {objPath} (Vertices: {vertices.Count}, Triangles: {triangles.Count / 3})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ImportFromOBJ failed: {ex.Message}");
            }
        }
    }
}
