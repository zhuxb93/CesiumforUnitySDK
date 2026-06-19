using CesiumForUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace GeoTiles.JobHelper
{
    /// <summary>
    /// 单个实例的动画数据：包含起始位置、当前插值位置、目标位置以及开始和结束时间。
    /// </summary>
    [BurstCompile]
    public struct AnimationFrameData
    {
        public double3 StartPosition;
        public double3 CurrentPosition;
        public double3 EndPosition;
        public float StartTime;
        public float EndTime;
    }

    /// <summary>
    /// 存储实例的位置信息、缩放、模型旋转、广告牌旋转等。
    /// </summary>
    public struct SingleObjectData : IComparable<SingleObjectData>
    {
        public float3 Position;
        public float3 Scale;
        public quaternion ModelRotation;
        public quaternion BillboardRotation;

        public int CompareTo(SingleObjectData other)
        {
            return (int)math.sign(Scale.x - other.Scale.x);
        }
    }

    /// <summary>
    /// 用于排序和比较的数据结构，一般记录距离或其他浮点值。
    /// originalIndex 用来映射回原始数据下标。
    /// </summary>
    public struct SortFloatData : IComparable<SortFloatData>, IEquatable<SortFloatData>
    {
        public float data;
        public int originalIndex;

        public int CompareTo(SortFloatData other)
        {
            return (int)math.sign((data - other.data));
        }

        public bool Equals(SortFloatData other)
        {
            return originalIndex == other.originalIndex;
        }
    }


    /// <summary>
    /// 根据动画帧数据进行插值计算，并转换到本地坐标系的 Job。
    /// </summary>
    [BurstCompile]
    public struct AnimateToLocalPosition : IJobFor
    {
        public NativeArray<SingleObjectData> SingleObjectDatas;
        public NativeArray<AnimationFrameData> FrameDatas;

        [ReadOnly]
        public double4x4 EcefToLocalMatrix;
        [ReadOnly]
        public float CurrentTime;

        public void Execute(int index)
        {
            var frameData = FrameDatas[index];

            // 判断是否在动画时间内
            if (CurrentTime < frameData.StartTime || CurrentTime > frameData.EndTime)
            {
                return;
            }

            // 计算插值因子 [0,1]
            float t = math.saturate((CurrentTime - frameData.StartTime) / (frameData.EndTime - frameData.StartTime));
            double3 interpolatedPosition = math.lerp(frameData.StartPosition, frameData.EndPosition, t);

            double3 lastPosition = frameData.CurrentPosition;
            // 更新当前插值位置
            frameData.CurrentPosition = interpolatedPosition;
            FrameDatas[index] = frameData;

            // 将经纬度高转换为 ECEF，再转换到本地坐标系
            double3 curEcefPosition = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(interpolatedPosition);
            double3 curLocalPosition = CoordinateConverter.Transform(EcefToLocalMatrix, curEcefPosition);

            unsafe
            {
                SingleObjectData* pSingleObjects = (SingleObjectData*)SingleObjectDatas.GetUnsafePtr();
                var lastLocalPosition = pSingleObjects[index].Position;
                pSingleObjects[index].Position = (float3)curLocalPosition;
                var dir = (float3)(curLocalPosition - lastLocalPosition);
                // 如果移动距离大于一定阈值，则更新模型自身的“面向方向”
                if (math.length(dir) > 0.01f)
                {
                    pSingleObjects[index].ModelRotation = quaternion.LookRotationSafe(math.normalize(dir), math.up());
                }
            }
        }
    }

    /// <summary>
    /// 让对象面向相机并根据距离相机的距离自动缩放的 Job。
    /// </summary>
    [BurstCompile]
    public struct SingleObjectAutoFaceToCameraAndAutoScaleJob : IJobFor
    {
        public NativeArray<SingleObjectData> SingleObjectDatas;
        [ReadOnly]
        public float3 CameraLocalPosition;
        [ReadOnly]
        public float3 CameraUpDir;

        public void Execute(int index)
        {
            unsafe
            {
                SingleObjectData* singleObject = ((SingleObjectData*)SingleObjectDatas.GetUnsafePtr()) + index;
                var dirToCamera = CameraLocalPosition - singleObject->Position;
                // 缩放值设置为距离相机的长度
                singleObject->Scale = math.length(dirToCamera);
                singleObject->BillboardRotation = quaternion.LookRotationSafe(math.normalize(dirToCamera), CameraUpDir);
            }
        }
    }

    /// <summary>
    /// 设置可见性数组：在 [0, VisibleCount) 范围内的标记为可见，其他为不可见。
    /// </summary>
    [BurstCompile]
    public struct SetVisibleJob : IJobFor
    {
        [WriteOnly]
        public NativeArray<byte> VisibleArray;

        public int VisibleCount;

        public void Execute(int index)
        {
            VisibleArray[index] = index < VisibleCount ? (byte)1 : (byte)0;
        }
    }

    /// <summary>
    /// 查找距离阈值内的对象并标记到 NearInvisibleObjects 数组中。
    /// </summary>
    [BurstCompile]
    public struct FindInvisibleObjectJob : IJobFor
    {
        public const int kNearInvisibleCounterIndex = 0;
        public const int kInvisibleCounterLength = 1;

        [ReadOnly]
        public NativeArray<SingleObjectData> SingleObjectDatas;
        [NativeDisableParallelForRestriction]
        public NativeArray<SortFloatData> NearInvisibleObjects;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> InvisibleCounters;

        [ReadOnly]
        public int NearInvisibleObjectsMaxLenth;

        [ReadOnly]
        public float MinimumVisibleDistance;

        public void Execute(int index)
        {
            float distance = SingleObjectDatas[index].Scale.x;
            unsafe
            {
                int* pInvisibleCounter = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(InvisibleCounters);
                if (distance < MinimumVisibleDistance)
                {
                    // 线程安全地累加
                    int outIndex = Interlocked.Add(ref pInvisibleCounter[kNearInvisibleCounterIndex], 1) - 1;
                    if (outIndex < NearInvisibleObjectsMaxLenth - 1)
                    {
                        SortFloatData* sortData = ((SortFloatData*)NearInvisibleObjects.GetUnsafePtr()) + outIndex;
                        sortData->originalIndex = index;
                        sortData->data = distance;
                    }
                    else
                    {
                        // 如果超出数组长度，就进行简单的“替换”策略
                        outIndex = outIndex % NearInvisibleObjectsMaxLenth;
                        SortFloatData* sortData = ((SortFloatData*)NearInvisibleObjects.GetUnsafePtr()) + outIndex;
                        if (sortData->data > distance)
                        {
                            sortData->originalIndex = index;
                            sortData->data = distance;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 查找上一帧的缓存，并将可以复用的对象分到 IsCacheArray，不能复用的对象分到 NoCacheArray。
    /// </summary>
    [BurstCompile]
    public struct FindLastFrameCacheJob : IJobFor
    {
        public const int kIsFIndCacheIndex = 0;
        public const int kNoFindCacheIndex = 1;
        public const int kCacheCounterLength = 4;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<SortFloatData> LastFarameData;

        public NativeArray<SortFloatData> NearInvisibleObjects;

        [NativeDisableParallelForRestriction]
        public NativeArray<SortFloatData> IsCacheArray;

        [NativeDisableParallelForRestriction]
        public NativeArray<SortFloatData> NoCacheArray;

        [NativeDisableParallelForRestriction]
        public NativeArray<int> CacheCounters;

        public void Execute(int index)
        {
            SortFloatData data = NearInvisibleObjects[index];
            // 判断当前 data 是否在上一帧中出现过
            bool isCache = LastFarameData.Contains(data);

            unsafe
            {
                int* pCacheCounters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(CacheCounters);
                if (isCache)
                {
                    int outIndex = Interlocked.Add(ref pCacheCounters[kIsFIndCacheIndex], 1) - 1;
                    if (outIndex < IsCacheArray.Length)
                    {
                        IsCacheArray[outIndex] = data;
                    }
                }
                else
                {
                    int outIndex = Interlocked.Add(ref pCacheCounters[kNoFindCacheIndex], 1) - 1;
                    if (outIndex < NoCacheArray.Length)
                    {
                        NoCacheArray[outIndex] = data;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 更新上一帧缓存，决定哪些原对象继续用 Prefab 替换，哪些释放掉。
    /// </summary>
    [BurstCompile]
    public struct UpdateFrameCacheJob : IJobFor
    {
        public const int kIsWriteCacheIndex = 2;
        public const int kNoWriteCacheIndex = 3;

        public NativeArray<SortFloatData> LastFarameData;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<SortFloatData> IsCacheArray;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<SortFloatData> NoCacheArray;

        [NativeDisableParallelForRestriction]
        public NativeArray<int> CacheCounters;

        public void Execute(int index)
        {
            unsafe
            {
                SortFloatData* pDatas = (SortFloatData*)LastFarameData.GetUnsafePtr();
                // 判断当前的缓存数据是否还在本帧使用
                bool isCache = IsCacheArray.Contains(pDatas[index]);
                int* pCacheCounters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(CacheCounters);

                // 如果不在本帧继续使用，就需要从 NoCacheArray 里找数据更新
                if (!isCache)
                {
                    int outIndex = Interlocked.Add(ref pCacheCounters[kNoWriteCacheIndex], 1) - 1;
                    if (outIndex < CacheCounters[FindLastFrameCacheJob.kNoFindCacheIndex])
                    {
                        pDatas[index] = NoCacheArray[outIndex];
                    }
                    else
                    {
                        // 标记为 -1，表示不再使用
                        pDatas[index].originalIndex = -1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 更新 Prefab Transform（位置、旋转等）的并行作业。
    /// </summary>
    [BurstCompile]
    public struct UpdatePrefabTransformJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<SingleObjectData> SingleObjectDatas;

        [ReadOnly]
        public NativeArray<SortFloatData> SortData;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> VisibleArray;

        public void Execute(int index, TransformAccess transform)
        {
            if (index >= SortData.Length)
            {
                return;
            }

            int originalIndex = SortData[index].originalIndex;
            if (originalIndex >= 0)
            {
                SingleObjectData singleObject = SingleObjectDatas[originalIndex];
                transform.localPosition = singleObject.Position;
                // 这里如有需要可修改缩放系数
                // transform.localScale = singleObject.Scale * 0.02f;
                transform.localRotation = singleObject.ModelRotation;

                // 在可见性数组中将原始对象标记为不可见（因为用 Prefab 替代了）
                VisibleArray[originalIndex] = 0;
            }
        }
    }

    /// <summary>
    /// 将 SingleObjectData 转换为本地变换矩阵的作业。
    /// </summary>
    [BurstCompile]
    public struct SingleObjectToLocalTransformJob : IJobFor
    {
        [ReadOnly]
        public NativeArray<SingleObjectData> SingleObjectDatas;

        [WriteOnly]
        public NativeArray<float4x4> LocalTransform;

        public void Execute(int index)
        {
            SingleObjectData singleObject = SingleObjectDatas[index];
            LocalTransform[index] = float4x4.TRS(
                singleObject.Position,
                singleObject.BillboardRotation,
                singleObject.Scale * 0.02f
            );
        }
    }

}
