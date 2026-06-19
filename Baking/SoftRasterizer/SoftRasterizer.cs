using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoTiles
{
    public static class SoftRasterizer
    {
        public static List<T> ColorToTexture<T>(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, List<T> colors, int textureW, int textureH)
        {
            // 参数合法性校验
            if (vertices == null || uvs == null || triangles == null || colors == null)
                throw new ArgumentNullException("输入参数不能为 null");
            if (vertices.Count != uvs.Count || vertices.Count != colors.Count)
                throw new ArgumentException("vertices, uvs, colors 长度必须一致");
            if (textureW <= 0 || textureH <= 0)
                throw new ArgumentException("纹理尺寸必须大于 0");

            // 初始化输出
            var output = new List<T>(textureW * textureH);
            for (int i = 0; i < textureW * textureH; i++)
                output.Add(default);

            // 遍历每个三角形
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                if (i0 >= uvs.Count || i1 >= uvs.Count || i2 >= uvs.Count)
                    continue;

                Vector2 uv0 = uvs[i0] * new Vector2(textureW, textureH);
                Vector2 uv1 = uvs[i1] * new Vector2(textureW, textureH);
                Vector2 uv2 = uvs[i2] * new Vector2(textureW, textureH);

                T c0 = colors[i0];
                T c1 = colors[i1];
                T c2 = colors[i2];

                RectInt bounds = GetBoundingBoxInt(uv0, uv1, uv2, textureW, textureH);

                for (int y = bounds.yMin; y <= bounds.yMax; y++)
                {
                    for (int x = bounds.xMin; x <= bounds.xMax; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        if (Barycentric(p, uv0, uv1, uv2, out Vector3 bc))
                        {
                            int idx = y * textureW + x;
                            output[idx] = Interpolate(c0, c1, c2, bc);
                        }
                    }
                }
            }

            return output;
        }

        private static RectInt GetBoundingBoxInt(Vector2 a, Vector2 b, Vector2 c, int w, int h)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), 0, w - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)), 0, w - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), 0, h - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)), 0, h - 1);
            return new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        private static bool Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 bc)
        {
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = p - a;

            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);

            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-5f)
            {
                bc = Vector3.zero;
                return false;
            }

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;

            bc = new Vector3(u, v, w);
            return u >= 0 && v >= 0 && w >= 0;
        }

        private static T Interpolate<T>(T c0, T c1, T c2, Vector3 bc)
        {
            try
            {
                if (typeof(T) == typeof(Vector3))
                {
                    Vector3 v0 = (Vector3)(object)c0;
                    Vector3 v1 = (Vector3)(object)c1;
                    Vector3 v2 = (Vector3)(object)c2;
                    return (T)(object)(v0 * bc.x + v1 * bc.y + v2 * bc.z);
                }
                else if (typeof(T) == typeof(float))
                {
                    float f0 = (float)(object)c0;
                    float f1 = (float)(object)c1;
                    float f2 = (float)(object)c2;
                    return (T)(object)(f0 * bc.x + f1 * bc.y + f2 * bc.z);
                }
                else if (typeof(T) == typeof(int))
                {
                    float f0 = Convert.ToSingle(c0);
                    float f1 = Convert.ToSingle(c1);
                    float f2 = Convert.ToSingle(c2);
                    return (T)(object)Mathf.RoundToInt(f0 * bc.x + f1 * bc.y + f2 * bc.z);
                }
                else
                {
                    throw new NotSupportedException($"不支持对类型 {typeof(T)} 做插值。");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"插值失败: {ex.Message}", ex);
            }
        }
    }
}
