using CesiumForUnity;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using GeoTiles.JobHelper;
using Unity.Burst;

namespace GeoTiles
{
    /// <summary>
    /// 使用 GPU Instancing 和 Job System 来高效地管理、实例化和动画化多个对象。
    /// </summary>
    public class InstanceObjectManager : MonoBehaviour
    {
        [Header("实例配置")]
        [Tooltip("用于实例化的网格")]
        public Mesh meshToInstantiate;

        [Tooltip("用于实例化的材质")]
        public Material materialForInstances;

        [Tooltip("实例是否投射阴影")]
        public bool shouldCastShadows = false;

        [Tooltip("允许创建的最大实例数量")]
        public int maxInstances = 100000;

        [Tooltip("Job 调度时的批处理大小")]
        public int jobBatchSize = 64;

        [Header("测试数据设置")]
        [Tooltip("是否启用测试数据生成")]
        public bool enableTestDataGeneration = false;

        [Header("使用 Prefab 替换")]
        [Tooltip("是否在一定距离内用 Prefab 替换对象")]
        public bool replaceNearObjectsWithPrefab = true;

        [Tooltip("Prefab 替换的距离阈值")]
        public float replacePrefabDistanceThreshold = 3000;

        [Tooltip("同时允许的最大 Prefab 替换数量")]
        public int maxPrefabs = 256;

        [Tooltip("用于替换的 Prefab 预制体")]
        public GameObject prefabForReplace;

        private InstanceContainer m_InstanceContainer;                       // BRG 容器类，用于管理 GPU Instancing
        private CesiumGlobeAnchor m_CesiumGlobeAnchor;                       // CesiumGlobeAnchor，用于地理坐标转换

        private Dictionary<string, int> m_ObjectNameToIndexMap = new Dictionary<string, int>(); // 对象名称与索引的映射
        private NativeArray<AnimationFrameData> m_AnimationFrameData;                           // 存储所有实例动画数据的数组
        private NativeArray<SingleObjectData> m_SingleObjectDataArray;                          // 存储单个对象数据（位置、缩放、朝向等）
        private NativeArray<SortFloatData> m_LastFramePrefabSortingData;                        // 上一帧 Prefab 排序数据

        private int m_CurrentInstanceCount = 0;    // 当前激活的实例数量
        private List<GameObject> m_PrefabInstances = new List<GameObject>(); // 用于替换的 Prefab 列表
        private TransformAccessArray m_PrefabTransformAccessArray;           // 用于并行操作 Transform 的 TransformAccessArray

        private const int k_MaxNearInvisibleObjectsLength = 1000; // 检测距离内最多可标记的待替换对象数量

        /// <summary>
        /// 初始化本脚本所需的 NativeArray 和其他必要数据结构。
        /// </summary>
        private void InitializeData()
        {
            m_AnimationFrameData = new NativeArray<AnimationFrameData>(maxInstances, Allocator.Persistent);
            m_LastFramePrefabSortingData = new NativeArray<SortFloatData>(maxPrefabs, Allocator.Persistent);
            m_SingleObjectDataArray = new NativeArray<SingleObjectData>(maxInstances, Allocator.Persistent);

            // 如果开启了 Prefab 替换功能且存在对应的替换预制体
            if (replaceNearObjectsWithPrefab && prefabForReplace != null)
            {
                m_PrefabTransformAccessArray = new TransformAccessArray(maxPrefabs);
                for (int i = 0; i < maxPrefabs; i++)
                {
                    GameObject go = Instantiate(prefabForReplace);
                    go.transform.SetParent(transform, false);
                    go.SetActive(false);
                    m_PrefabInstances.Add(go);
                    m_PrefabTransformAccessArray.Add(go.transform);

                    unsafe
                    {
                        // 将 originalIndex 初始化为 -1，表示“未使用”
                        ((SortFloatData*)m_LastFramePrefabSortingData.GetUnsafePtr())[i].originalIndex = -1;
                    }
                }
            }
        }

        /// <summary>
        /// 生成一个简单的 Billboard 网格（可视为一个平面），供 meshToInstantiate 为 null 时使用
        /// </summary>
        private static Mesh CreateBillboardMesh()
        {
            Mesh billboardMesh = new Mesh();

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };

            int[] triangles = new int[]
            {
                1, 0, 2,
                2, 0, 3
            };

            billboardMesh.vertices = vertices;
            billboardMesh.uv = uvs;
            billboardMesh.triangles = triangles;
            billboardMesh.RecalculateNormals();
            billboardMesh.RecalculateBounds();

            return billboardMesh;
        }

        /// <summary>
        /// Unity Start：初始化数据、创建材质和 BRG 容器。
        /// </summary>
        void Start()
        {
            // 尝试获取同 GameObject 上的 CesiumGlobeAnchor
            m_CesiumGlobeAnchor = GetComponent<CesiumGlobeAnchor>();
            // 如果没有则尝试从父物体上获取
            if (m_CesiumGlobeAnchor == null)
            {
                m_CesiumGlobeAnchor = GetComponentInParent<CesiumGlobeAnchor>();
            }

            // 初始化本脚本所需数据
            InitializeData();

            // 如果 meshToInstantiate 为 null，自动生成一个简易 Billboard Mesh
            if (meshToInstantiate == null)
            {
                Debug.LogWarning("meshToInstantiate 为空，自动生成 Billboard Mesh。");
                meshToInstantiate = CreateBillboardMesh();
            }

            // 如果 materialForInstances 为 null，自动生成一个默认的 URP Lit 材质
            if (materialForInstances == null)
            {
                Debug.LogWarning("materialForInstances 为空，自动生成默认 URP Lit 材质。");
                Shader defaultLitShader = Shader.Find("Universal Render Pipeline/Lit");
                if (defaultLitShader != null)
                {
                    materialForInstances = new Material(defaultLitShader);
                }
                else
                {
                    // 如果项目里没有导入 URP，则使用Standard
                    materialForInstances = new Material(Shader.Find("Standard"));
                }
            }
            else
            {
                // 这里克隆材质，以免对原始材质产生影响
                materialForInstances = Instantiate(materialForInstances);
            }

            // 创建并初始化 BRG 容器
            m_InstanceContainer = new InstanceContainer(meshToInstantiate, materialForInstances, maxInstances);
        }

        /// <summary>
        /// Unity Update：每帧执行，对动画和可见性等数据进行处理并更新到 GPU。
        /// </summary>
        private void Update()
        {
            // 如果 Mesh 或材质为空，直接返回
            if (meshToInstantiate == null || materialForInstances == null)
            {
                return;
            }

            // 如果容器还未创建，直接返回
            if (m_InstanceContainer == null)
            {
                return;
            }

            JobHandle jobHandle = new JobHandle();

            // 如果启用测试数据生成，就创建一定数量的测试实例
            if (enableTestDataGeneration)
            {
                // 这里仅作演示，让当前激活数量略小于最大值
                m_CurrentInstanceCount = maxInstances - 100;

                // 调用测试 Job，为动画数据填充随机值
                jobHandle = new FillTestAnimationDataJob
                {
                    FrameDatas = m_AnimationFrameData,
                    CurrentTime = Time.time,
                }.ScheduleParallel(m_CurrentInstanceCount, jobBatchSize, jobHandle);
            }

            // 同步 CesiumGlobeAnchor，确保坐标转换正确
            //m_CesiumGlobeAnchor.Sync();

            NativeArray<SortFloatData> nearInvisibleDatas = new NativeArray<SortFloatData>(
                k_MaxNearInvisibleObjectsLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory
            );
            NativeArray<int> invisibleCounter = new NativeArray<int>(
                FindInvisibleObjectJob.kInvisibleCounterLength, Allocator.TempJob
            );
            NativeArray<SortFloatData> isCacheArray = new NativeArray<SortFloatData>(
                maxPrefabs, Allocator.TempJob, NativeArrayOptions.UninitializedMemory
            );
            NativeArray<SortFloatData> noCacheArray = new NativeArray<SortFloatData>(
                k_MaxNearInvisibleObjectsLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory
            );
            NativeArray<int> cacheCounter = new NativeArray<int>(
                FindLastFrameCacheJob.kCacheCounterLength, Allocator.TempJob
            );

            // 获取主相机信息并转换到当前物体的局部坐标系中
            var cameraTransform = Camera.main.transform;
            var vec4CamLocalPos = transform.worldToLocalMatrix * new Vector4(
                cameraTransform.position.x,
                cameraTransform.position.y,
                cameraTransform.position.z,
                1.0f
            );
            var float3CamPos = ((float4)vec4CamLocalPos).xyz;

            var vec4CamLocalUp = transform.worldToLocalMatrix * new Vector4(
                cameraTransform.up.x,
                cameraTransform.up.y,
                cameraTransform.up.z,
                0.0f
            );
            var float3CamUp = ((float4)vec4CamLocalUp).xyz;

            // 本地到世界的变换矩阵
            float4x4 localToWorld = float4x4.TRS(transform.position, transform.rotation, transform.localScale);

            // 调度动画补间 Job：计算每个实例当前的插值位置，并更新旋转朝向
            jobHandle = new AnimateToLocalPosition
            {
                FrameDatas = m_AnimationFrameData,
                SingleObjectDatas = m_SingleObjectDataArray,
                EcefToLocalMatrix = CoordinateConverter.GetEcefToLocalMatrix(m_CesiumGlobeAnchor.positionGlobeFixed),
                CurrentTime = Time.time
            }.ScheduleParallel(m_CurrentInstanceCount, jobBatchSize, jobHandle);

            // 调度面向相机和自适应缩放 Job：让对象朝向相机并根据到相机的距离进行缩放
            jobHandle = new SingleObjectAutoFaceToCameraAndAutoScaleJob
            {
                SingleObjectDatas = m_SingleObjectDataArray,
                CameraLocalPosition = float3CamPos,
                CameraUpDir = float3CamUp
            }.ScheduleParallel(m_CurrentInstanceCount, jobBatchSize, jobHandle);

            // 获取容器内部的可见性数组，并将当前激活数量内的实例标记为可见
            var visibleArray = m_InstanceContainer.GetVisibleArray();
            jobHandle = new SetVisibleJob
            {
                VisibleCount = m_CurrentInstanceCount,
                VisibleArray = visibleArray
            }.ScheduleParallel(visibleArray.Length, jobBatchSize, jobHandle);

            // 查找距离阈值内需要被 Prefab 替换的对象
            jobHandle = new FindInvisibleObjectJob
            {
                SingleObjectDatas = m_SingleObjectDataArray,
                NearInvisibleObjects = nearInvisibleDatas,
                InvisibleCounters = invisibleCounter,
                NearInvisibleObjectsMaxLenth = k_MaxNearInvisibleObjectsLength,
                MinimumVisibleDistance = replacePrefabDistanceThreshold
            }.ScheduleParallel(m_CurrentInstanceCount, jobBatchSize, jobHandle);

            // 等待前面 Job 全部完成再继续
            jobHandle.Complete();

            // 取出实际标记到的个数
            var nearInvisibleCount = invisibleCounter[FindInvisibleObjectJob.kNearInvisibleCounterIndex];
            nearInvisibleCount = math.min(nearInvisibleCount, k_MaxNearInvisibleObjectsLength);

            // 取得刚才标记的那部分数据进行 SortJob 排序
            var sortedDatas = nearInvisibleDatas.GetSubArray(0, nearInvisibleCount);
            jobHandle = sortedDatas.SortJob().Schedule(jobHandle);

            // 查找上一帧已经使用了 Prefab 替换的对象是否还能复用
            jobHandle = new FindLastFrameCacheJob
            {
                LastFarameData = m_LastFramePrefabSortingData,
                NearInvisibleObjects = sortedDatas,
                IsCacheArray = isCacheArray,
                NoCacheArray = noCacheArray,
                CacheCounters = cacheCounter
            }.ScheduleParallel(nearInvisibleCount, jobBatchSize, jobHandle);

            // 等待完成
            jobHandle.Complete();

            // 处理跟随目标（如果有）
            var keepTransformIndex = -1;
            SortFloatData keepObject = new SortFloatData { data = 0, originalIndex = -1 };
            var followTarget = CesiumLoadingManager.Instance.geoTilesCesiumCameraController.FollowTarget;
            if (followTarget != null)
            {
                for (int i = 0; i < maxPrefabs; ++i)
                {
                    if (followTarget == m_PrefabInstances[i].transform)
                    {
                        keepTransformIndex = i;
                        keepObject = m_LastFramePrefabSortingData[i];
                        break;
                    }
                }
            }

            // 更新帧缓存（即上一帧哪些对象用 Prefab 替换，是否还能继续复用）
            jobHandle = new UpdateFrameCacheJob
            {
                LastFarameData = m_LastFramePrefabSortingData,
                IsCacheArray = isCacheArray,
                NoCacheArray = noCacheArray,
                CacheCounters = cacheCounter
            }.ScheduleParallel(maxPrefabs, jobBatchSize, jobHandle);

            jobHandle.Complete();

            // 如果有跟随目标，则保留这个目标对应的 transform
            if (keepTransformIndex >= 0)
            {
                m_LastFramePrefabSortingData[keepTransformIndex] = keepObject;
            }

            // 更新 Prefab Transform（位置、旋转等）
            jobHandle = new UpdatePrefabTransformJob
            {
                SingleObjectDatas = m_SingleObjectDataArray,
                SortData = m_LastFramePrefabSortingData,
                VisibleArray = m_InstanceContainer.GetVisibleArray()
            }.Schedule(m_PrefabTransformAccessArray, jobHandle);

            // 根据是否在本帧继续使用，来显示或隐藏对应的 Prefab
            for (int i = 0; i < maxPrefabs; ++i)
            {
                GameObject go = m_PrefabInstances[i];
                bool isVisible = m_LastFramePrefabSortingData[i].originalIndex >= 0;
                go.SetActive(isVisible);
            }

            // 将单个对象数据同步到 BRG 容器的 localTransform 中
            jobHandle = new SingleObjectToLocalTransformJob
            {
                SingleObjectDatas = m_SingleObjectDataArray,
                LocalTransform = m_InstanceContainer.GetLocalTransforms()
            }.ScheduleParallel(m_CurrentInstanceCount, jobBatchSize, jobHandle);

            // 通知容器进行渲染数据的调度
            jobHandle = m_InstanceContainer.ScheduleParallel(localToWorld, m_CurrentInstanceCount, jobBatchSize, jobHandle);

            jobHandle.Complete();

            // 将 CPU 端的数据上传到 GPU
            m_InstanceContainer.UploadGpuData();

            // 释放临时 NativeArray
            if (nearInvisibleDatas.IsCreated)
                nearInvisibleDatas.Dispose();
            if (sortedDatas.IsCreated)
                sortedDatas.Dispose();
            if (invisibleCounter.IsCreated)
                invisibleCounter.Dispose();
            if (isCacheArray.IsCreated)
                isCacheArray.Dispose();
            if (noCacheArray.IsCreated)
                noCacheArray.Dispose();
            if (cacheCounter.IsCreated)
                cacheCounter.Dispose();
        }

        /// <summary>
        /// Unity OnDestroy：清理申请的 NativeArray 和 BRG 容器。
        /// </summary>
        private void OnDestroy()
        {
            if (m_AnimationFrameData.IsCreated)
                m_AnimationFrameData.Dispose();

            if (m_SingleObjectDataArray.IsCreated)
                m_SingleObjectDataArray.Dispose();

            if (m_LastFramePrefabSortingData.IsCreated)
                m_LastFramePrefabSortingData.Dispose();

            if (m_PrefabTransformAccessArray.isCreated)
                m_PrefabTransformAccessArray.Dispose();

            if (m_InstanceContainer != null)
                m_InstanceContainer.Dispose();

            if (materialForInstances != null)
                Destroy(materialForInstances);

            // 销毁所有创建的 Prefab 实例
            for (int i = 0; i < m_PrefabInstances.Count; ++i)
            {
                if (m_PrefabInstances[i] != null)
                {
                    Destroy(m_PrefabInstances[i]);
                }
            }
        }

        /// <summary>
        /// 添加或更新一个实例。
        /// </summary>
        /// <param name="objectName">对象名称，必须唯一。</param>
        /// <param name="longitudeLatitudeHeight">地理位置：经度、纬度、高度。</param>
        /// <param name="duration">动画持续时长。</param>
        public void AddOrUpdateInstance(string objectName, double3 longitudeLatitudeHeight, float duration = 1f)
        {
            if (m_ObjectNameToIndexMap.ContainsKey(objectName))
            {
                UpdateInstance(objectName, longitudeLatitudeHeight, duration);
            }
            else
            {
                AddInstance(objectName, longitudeLatitudeHeight, duration);
            }
        }

        /// <summary>
        /// 更新已经存在的实例的动画数据。
        /// </summary>
        /// <param name="objectName">对象名称。</param>
        /// <param name="longitudeLatitudeHeight">新的目标位置。</param>
        /// <param name="duration">动画持续时长。</param>
        private void UpdateInstance(string objectName, double3 longitudeLatitudeHeight, float duration = 1f)
        {
            int index = m_ObjectNameToIndexMap[objectName];
            var currentFrameData = m_AnimationFrameData[index].CurrentPosition;

            m_AnimationFrameData[index] = new AnimationFrameData
            {
                StartPosition = currentFrameData,
                CurrentPosition = currentFrameData,
                EndPosition = longitudeLatitudeHeight,
                StartTime = Time.time,
                EndTime = Time.time + duration
            };
        }

        /// <summary>
        /// 添加一个新的实例。
        /// </summary>
        /// <param name="objectName">对象名称，必须唯一。</param>
        /// <param name="longitudeLatitudeHeight">地理位置：经度、纬度、高度。</param>
        /// <param name="duration">动画持续时长。</param>
        private void AddInstance(string objectName, double3 longitudeLatitudeHeight, float duration = 1f)
        {
            if (m_CurrentInstanceCount >= maxInstances)
            {
                Debug.LogError("实例数量已达上限，无法继续添加。");
                return;
            }

            m_ObjectNameToIndexMap.Add(objectName, m_CurrentInstanceCount);
            m_AnimationFrameData[m_CurrentInstanceCount] = new AnimationFrameData
            {
                StartPosition = longitudeLatitudeHeight,
                CurrentPosition = longitudeLatitudeHeight,
                EndPosition = longitudeLatitudeHeight,
                StartTime = Time.time,
                EndTime = Time.time + duration
            };

            m_CurrentInstanceCount++;
        }

        /// <summary>
        /// 测试数据填充 Job：只在 enableTestDataGeneration = true 时生效，为实例生成随机动画数据。
        /// </summary>
        [BurstCompile]
        public struct FillTestAnimationDataJob : IJobFor
        {
            public NativeArray<AnimationFrameData> FrameDatas;
            public float CurrentTime;

            public void Execute(int index)
            {
                var frameData = FrameDatas[index];

                // 如果动画尚未结束，则跳过填充
                if (CurrentTime < frameData.EndTime)
                {
                    return;
                }

                // 每个实例用一个不同的随机数种子
                // 使用系统时间的Ticks + index + CurrentTime来生成一个更加唯一的种子
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(
                    (uint)(index + (long)CurrentTime)
                );

                double latitudeStart = 31.5 + random.NextDouble() * 1;
                double longitudeStart = 116.9 + random.NextDouble() * 1;
                double altitudeStart = random.NextDouble() * 500.0; // 高度范围 0 ~ 500 米

                double3 startPosition = new double3(longitudeStart, latitudeStart, altitudeStart);

                // 生成一个随机目标点
                double latitude = 31.5 + random.NextDouble() * 1;
                double longitude = 116.9 + random.NextDouble() * 1;
                double altitude = random.NextDouble() * 500.0;

                double3 endPosition = new double3(longitude, latitude, altitude);

                float endTime = CurrentTime + (random.NextFloat() * 10000.0f + 5000.0f);

                FrameDatas[index] = new AnimationFrameData
                {
                    StartPosition = startPosition,
                    CurrentPosition = startPosition,
                    EndPosition = endPosition,
                    StartTime = CurrentTime,
                    EndTime = endTime
                };
            }
        }

    }
}
