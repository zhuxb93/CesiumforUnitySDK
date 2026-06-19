using CesiumForUnity;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public static class TileCalculator
    {
        private const int EARTH_RADIUS = 6378137;
        private const double PI = Math.PI;

        private static readonly Dictionary<int, double> LevelToSpanCache = new Dictionary<int, double>();
        private static readonly Dictionary<int, int> LevelToNCache = new Dictionary<int, int>();

        public static TileID LonLatToTile4326(double lat, double lon, int z)
        {
            return LonLatToTile(lon, lat, z);
        }

        public static TileID LonLatToTile(double lon, double lat, int level)
        {
            int n = GetN(level);
            double tempInterval = 180.0 / n;
            double mx = lon + 180.0;
            double my = lat + 90.0;
            mx /= tempInterval;
            my /= tempInterval;

            var tileID = TileID.Create();
            tileID.x = (uint)(int)mx;
            tileID.y = (uint)(int)my;
            tileID.z = (uint)level;
            return tileID;
        }

        public static double2 Get4326TileCenterPosition(TileID tileID)
        {
            return GetTileCenter(tileID);
        }

        public static double2 GetTileCenter(TileID tileID)
        {
            double span = GetSpan((int)tileID.z);
            double lon = tileID.x * span - 180.0 + span * 0.5;
            double lat = tileID.y * span - 90.0 + span * 0.5;
            return new double2(lon, lat);
        }

        public static (double2 topLeft, double2 bottomRight) GetTileBounds(TileID tileID)
        {
            double span = GetSpan((int)tileID.z);
            double halfSpan = span * 0.5;
            
            double2 center = GetTileCenter(tileID);
            
            return (
                new double2(center.x - halfSpan, center.y + halfSpan),
                new double2(center.x + halfSpan, center.y - halfSpan)
            );
        }

        public static Vector3 GetPolygonCenter(Vector3[] points)
        {
            if (points == null || points.Length == 0) return Vector3.zero;
            
            Vector3 center = Vector3.zero;
            int count = points.Length;
            
            for (int i = 0; i < count; i++)
            {
                center += points[i];
            }
            
            return center / count;
        }

        public static Vector3 GetPolygonCenter(Vector3[] points, Vector3 offset)
        {
            if (points == null || points.Length == 0) return Vector3.zero;
            
            Vector3 center = Vector3.zero;
            int count = points.Length;
            
            for (int i = 0; i < count; i++)
            {
                center += points[i] + offset;
            }
            
            return center / count;
        }

        public static double2 GetCenterCoordinate(double2 topLeft, double2 bottomRight)
        {
            return new double2(
                (topLeft.x + bottomRight.x) * 0.5,
                (topLeft.y + bottomRight.y) * 0.5
            );
        }

        public static bool IsPointInsidePolygon(Vector3 point, List<Vector3> polygon)
        {
            int intersections = 0;
            int count = polygon.Count;

            for (int i = 0; i < count; i++)
            {
                Vector3 p1 = polygon[i];
                Vector3 p2 = polygon[(i + 1) % count];

                if (p1.z == p2.z) continue;
                if (point.z < Mathf.Min(p1.z, p2.z) || point.z >= Mathf.Max(p1.z, p2.z)) continue;

                float x = (point.z - p1.z) * (p2.x - p1.x) / (p2.z - p1.z) + p1.x;
                if (x > point.x) intersections++;
            }

            return intersections % 2 != 0;
        }

        public static bool CalculatePointInsidePolygon(Vector3 point, List<Vector3> vertices)
        {
            int count = vertices.Count;
            if (count == 0) return false;

            int leftmostIndex = 0;
            for (int i = 1; i < count; i++)
            {
                if (vertices[i].x < vertices[leftmostIndex].x)
                    leftmostIndex = i;
            }

            var polygon = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                polygon.Add(vertices[(leftmostIndex + i) % count]);
            }

            return IsPointInsidePolygon(point, polygon);
        }

        public static HashSet<TileID> GetTilesInRange(double2 centerLonLat, int level, float xRangeKm, float yRangeKm)
        {
            double lonOffset = LongitudeOffset(centerLonLat.y, xRangeKm);
            double latOffset = LatitudeOffset(yRangeKm);

            double minLon = centerLonLat.x - lonOffset;
            double maxLon = centerLonLat.x + lonOffset;
            double minLat = centerLonLat.y - latOffset;
            double maxLat = centerLonLat.y + latOffset;

            TileID minTile = LonLatToTile(minLon, minLat, level);
            TileID maxTile = LonLatToTile(maxLon, maxLat, level);

            return GenerateTileRange(minTile, maxTile);
        }

        public static HashSet<TileID> GenerateTileRange(TileID minTile, TileID maxTile)
        {
            var result = new HashSet<TileID>();
            
            uint minX = Math.Min(minTile.x, maxTile.x);
            uint maxX = Math.Max(minTile.x, maxTile.x);
            uint minY = Math.Min(minTile.y, maxTile.y);
            uint maxY = Math.Max(minTile.y, maxTile.y);

            int countX = (int)(maxX - minX + 1);
            int countY = (int)(maxY - minY + 1);
            
            result.EnsureCapacity(countX * countY);

            for (uint x = minX; x <= maxX; x++)
            {
                for (uint y = minY; y <= maxY; y++)
                {
                    var tile = TileID.Create();
                    tile.x = x;
                    tile.y = y;
                    tile.z = minTile.z;
                    result.Add(tile);
                }
            }

            return result;
        }

        public static HashSet<TileID> GenerateTileRange(TileID minTile, TileID maxTile,uint level)
        {
            var result = new HashSet<TileID>();
            
            uint minX = Math.Min(minTile.x, maxTile.x);
            uint maxX = Math.Max(minTile.x, maxTile.x);
            uint minY = Math.Min(minTile.y, maxTile.y);
            uint maxY = Math.Max(minTile.y, maxTile.y);

            int countX = (int)(maxX - minX + 1);
            int countY = (int)(maxY - minY + 1);
            
            result.EnsureCapacity(countX * countY);

            for (uint x = minX; x <= maxX; x++)
            {
                for (uint y = minY; y <= maxY; y++)
                {
                    var tile = TileID.Create();
                    tile.x = x;
                    tile.y = y;
                    tile.z = level;
                    result.Add(tile);
                }
            }

            return result;
        }

        public static List<TileID> GetSurroundingTiles(TileID centerTile)
        {
            var result = new List<TileID>(9);
            result.Add(centerTile);

            int[] offsets = { -1, 0, 1 };
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (offsets[i] == 0 && offsets[j] == 0) continue;

                    var tile = TileID.Create();
                    tile.x = (uint)(centerTile.x + offsets[j]);
                    tile.y = (uint)(centerTile.y + offsets[i]);
                    tile.z = centerTile.z;
                    result.Add(tile);
                }
            }

            return result;
        }

        public static HashSet<TileID> GetSurroundingTiles(TileID centerTile, TileID topLeftTile, TileID bottomRightTile)
        {
            var result = new HashSet<TileID>();
            var queue = new Queue<TileID>();
            var visited = new HashSet<TileID>();

            queue.Enqueue(centerTile);
            visited.Add(centerTile);

            while (queue.Count > 0)
            {
                TileID tile = queue.Dequeue();
                result.Add(tile);

                var surrounding = GetSurroundingTiles(tile);
                foreach (var surroundingTile in surrounding)
                {
                    if (!visited.Contains(surroundingTile) &&
                        surroundingTile.x >= topLeftTile.x && 
                        surroundingTile.x <= bottomRightTile.x &&
                        surroundingTile.y >= bottomRightTile.y && 
                        surroundingTile.y <= topLeftTile.y)
                    {
                        queue.Enqueue(surroundingTile);
                        visited.Add(surroundingTile);
                    }
                }
            }

            return result;
        }

        private static int GetN(int level)
        {
            if (!LevelToNCache.TryGetValue(level, out int n))
            {
                n = 1 << level;
                LevelToNCache[level] = n;
            }
            return n;
        }

        private static double GetSpan(int level)
        {
            if (!LevelToSpanCache.TryGetValue(level, out double span))
            {
                span = 180.0 / GetN(level);
                LevelToSpanCache[level] = span;
            }
            return span;
        }

        private static double LongitudeOffset(double lat, double distKm)
        {
            double R = EARTH_RADIUS / 1000.0;
            double d = distKm / R;
            double cs = Math.Cos(lat * PI / 180.0);
            return d / cs * 180.0 / PI;
        }

        private static double LatitudeOffset(double distKm)
        {
            double R = EARTH_RADIUS / 1000.0;
            double d = distKm / R;
            return d * 180.0 / PI;
        }

        public static double3 GetTileEcefCenter(TileID tileID)
        {
            var lonLat = GetTileCenter(tileID);
            return CesiumConversions.Instance.ConvertLongitudeLatitudeHeightToECEF(lonLat.x, lonLat.y);
        }
    }
}