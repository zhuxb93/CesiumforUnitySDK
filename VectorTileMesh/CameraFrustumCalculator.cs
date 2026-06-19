using CesiumForUnity;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    /// <summary>
    /// 相机视锥体瓦片计算器
    /// 根据相机视锥体范围计算需要加载的瓦片
    /// </summary>
    public static class CameraFrustumCalculator
    {
        /// <summary>
        /// 获取相机视锥体内的瓦片
        /// </summary>
        public static HashSet<TileID> GetTilesInFrustum(
            Camera camera,
            GeoTilesCesiumCameraController cameraController,
            float range,
            int level)
        {
            var (minTile, maxTile) = CalculateFrustumTileRange(camera, cameraController, range, level);
            return TileCalculator.GenerateTileRange(minTile, maxTile);
        }

        /// <summary>
        /// 计算视锥体范围内的瓦片边界
        /// </summary>
        private static (TileID minTile, TileID maxTile) CalculateFrustumTileRange(
            Camera camera,
            GeoTilesCesiumCameraController cameraController,
            float range,
            int level)
        {
            // 根据倾斜角度计算距离因子
            float skewFactor = CalculateSkewFactor(cameraController);
            
            // 计算近裁剪面和远裁剪面距离
            float nearDistance = (float)cameraController.CameraToLookCenterDistance * 0.5f;
            float farDistance = (float)cameraController.CameraToLookCenterDistance / skewFactor;

            // 计算视锥体高度和宽度
            float nearHeight = GetFrustumHeight(camera, nearDistance);
            float farHeight = GetFrustumHeight(camera, farDistance);

            float nearWidth = nearHeight * camera.aspect;
            float farWidth = farHeight * camera.aspect;

            var transform = camera.transform;
            var position = transform.position;
            var forward = transform.forward;
            var right = transform.right;
            var up = transform.up;

            // 计算近裁剪面中心和远裁剪面中心
            var nearCenter = position + forward * nearDistance;
            var farCenter = position + forward * farDistance;

            // 根据倾斜角度调整远裁剪面的偏移
            float farOffsetFactor = CalculateSkewFactorInverse(cameraController);
            
            // 计算左上角世界坐标（基于远裁剪面）
            var topLeftWorld = farCenter 
                - right * (farWidth * 0.25f) 
                + up * (farHeight * 1.0f);
            topLeftWorld *= range;

            // 计算右下角世界坐标（基于近裁剪面）
            var bottomRightWorld = nearCenter 
                + right * (nearWidth * farOffsetFactor) 
                - up * (nearHeight * 1.0f);
            bottomRightWorld *= range * 2f;

            // 使用逆旋转矩阵还原坐标
            var rotationMatrix = Matrix4x4.TRS(Vector3.zero, transform.rotation, Vector3.one);
            var inverseRotation = rotationMatrix.inverse;

            var topLeftLocal = inverseRotation.MultiplyPoint3x4(topLeftWorld);
            var bottomRightLocal = inverseRotation.MultiplyPoint3x4(bottomRightWorld);
            bottomRightLocal += Vector3.back * 1000f;

            // 转换为经纬度
            var topLeftLonLat = CesiumConversions.Instance.ConvertWorldPositionToLongitudeLatitudeHeight(topLeftLocal);
            var bottomRightLonLat = CesiumConversions.Instance.ConvertWorldPositionToLongitudeLatitudeHeight(bottomRightLocal);

            // 确定经纬度范围（考虑相机旋转后左上角和右下角可能互换）
            double minLon = System.Math.Min(topLeftLonLat.x, bottomRightLonLat.x);
            double maxLon = System.Math.Max(topLeftLonLat.x, bottomRightLonLat.x);
            double minLat = System.Math.Min(topLeftLonLat.y, bottomRightLonLat.y);
            double maxLat = System.Math.Max(topLeftLonLat.y, bottomRightLonLat.y);

            // 转换为瓦片坐标
            var minTile = TileCalculator.LonLatToTile(minLon, minLat, level);
            var maxTile = TileCalculator.LonLatToTile(maxLon, maxLat, level);

            return (minTile, maxTile);
        }

        /// <summary>
        /// 计算指定距离下的视锥体高度
        /// </summary>
        private static float GetFrustumHeight(Camera camera, float distance)
        {
            return 2.0f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 计算倾斜因子（用于调整远裁剪面距离）
        /// 倾斜角度越大，远裁剪面距离越远
        /// </summary>
        private static float CalculateSkewFactor(GeoTilesCesiumCameraController controller)
        {
            float skewAngle = (float)controller.SkewAngle;
            float minAngle = 10f;
            float maxAngle = (float)controller.SkewAngleLiimt.y;
            
            float normalizedAngle = Mathf.Clamp01((skewAngle - minAngle) / (maxAngle - minAngle));
            return 0.5f - normalizedAngle * (0.5f - 0.35f);
        }

        /// <summary>
        /// 计算逆向倾斜因子（用于调整右下角偏移）
        /// </summary>
        private static float CalculateSkewFactorInverse(GeoTilesCesiumCameraController controller)
        {
            float skewAngle = (float)controller.SkewAngle;
            float minAngle = 10f;
            float maxAngle = (float)controller.SkewAngleLiimt.y;
            
            float normalizedAngle = Mathf.Clamp01((skewAngle - minAngle) / (maxAngle - minAngle));
            return 0.85f + normalizedAngle * (1.5f - 0.85f);
        }
    }
}