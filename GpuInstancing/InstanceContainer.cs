using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public class InstanceContainer : IDisposable
    {
        public Mesh InstanceMesh { get; private set; }
        public Material InstanceMaterial { get; private set; }
        public bool CastShadows = false;

        private BRGContainer m_BrgContainer;
        private NativeArray<float4x4> m_LocalTransforms;
        private NativeArray<byte> m_VisibleArray;

        private const int DEFAULT_CAPACITY = 1024;

        private int m_Capacity = 0;
        private int m_Size = 0;
        public InstanceContainer(Mesh mesh, Material material, int initialCapacity = DEFAULT_CAPACITY)
        {
            InstanceMesh = mesh;
            InstanceMaterial = material;
            m_Capacity = initialCapacity > 0 ? initialCapacity : DEFAULT_CAPACITY;
            Reserve(m_Capacity);
        }

        public NativeArray<float4x4> GetLocalTransforms()
        {
            return m_LocalTransforms;
        }
        public NativeArray<byte> GetVisibleArray()
        {
            return m_VisibleArray;
        }

        private void Reallocate(int newCapacity)
        {
            if (newCapacity <= 0)
                newCapacity = DEFAULT_CAPACITY;

            if (m_Capacity > 0)
            {
                ResizeArray(ref m_LocalTransforms, m_Capacity);
                ResizeArray(ref m_VisibleArray, m_Capacity);
            }

            if (m_BrgContainer != null)
            {
                m_BrgContainer.Shutdown();
                m_BrgContainer = null;
            }

            m_BrgContainer = new BRGContainer();
            var propertyDescriptions = new BRGContainer.PropertyDescription[]
            {
                new BRGContainer.PropertyDescription(Shader.PropertyToID("unity_ObjectToWorld"), 3),
                new BRGContainer.PropertyDescription(Shader.PropertyToID("unity_WorldToObject"), 3),
            };
            m_BrgContainer.Init(InstanceMesh, InstanceMaterial, newCapacity, propertyDescriptions, CastShadows);

            if (m_Size > 0)
            {
                m_BrgContainer.UploadGpuData(m_Size);
            }

            m_Capacity = newCapacity;
        }

        public void Reserve(int newCapacity = DEFAULT_CAPACITY)
        {
            if (newCapacity < 0)
                newCapacity = DEFAULT_CAPACITY;

            if (newCapacity > m_Capacity)
            {
                Reallocate(newCapacity);
            }
            else if( m_BrgContainer == null )
            {
                Reallocate(newCapacity);
            }
        }

        public void Resize(int newSize)
        {
            if (newSize < 0)
                newSize = 0;

            if (newSize > m_Capacity)
            {
                int newCapacity = math.max(newSize, m_Capacity * 2);
                Reallocate(newCapacity);
            }

            m_Size = newSize;
        }

        public int Add(in float4x4 localTransform)
        {
            if (m_Size == m_Capacity)
            {
                int newCapacity = m_Capacity * 2;
                Reallocate(newCapacity);
            }

            m_LocalTransforms[m_Size] = localTransform;
            m_VisibleArray[m_Size] = 1;
            return m_Size++;
        }

        public void Destroy()
        {
            if (m_LocalTransforms.IsCreated)
                m_LocalTransforms.Dispose();

            if (m_BrgContainer != null)
                m_BrgContainer.Shutdown();

            m_Size = 0;
            m_Capacity = 0;
        }
        public JobHandle ScheduleParallel(in float4x4 parentLocalToWorld = default, int arrayLength = -1,  int innerloopBatchCount = 64, JobHandle jobHandle = default)
        {
            if(arrayLength > 0)
            {
                arrayLength = arrayLength < m_Capacity ? arrayLength : m_Capacity;
                m_Size = arrayLength;
            }
            jobHandle = new SetInstanceDataJob
            {
                LocalTransform = m_LocalTransforms,
                VisibleArray = m_VisibleArray,
                ParentTransform = parentLocalToWorld,
                MaxInstancesPerWindow = m_BrgContainer.MaxInstancePerWindow,
                WindowSizeInFloat4 = m_BrgContainer.WindowSizeInFloat4,
                SysMemBuffer = m_BrgContainer.GetSysmemBuffer(),
            }.ScheduleParallel(m_Size, innerloopBatchCount, jobHandle);
            return jobHandle;
        }

        public bool UploadGpuData()
        {
            if (InstanceMesh == null || InstanceMaterial == null)
            {
                return false;
            }

            if (m_Size <= 0)
            {
                return false;
            }

            return m_BrgContainer.UploadGpuData(m_Size);
        }

        public void Dispose()
        {
            Destroy();
        }

        private static void ResizeArray<T>(ref NativeArray<T> array, int capacity, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            var newArray = new NativeArray<T>(capacity, Allocator.Persistent, options);
            if (array.IsCreated)
            {
                NativeArray<T>.Copy(array, newArray, array.Length);
                array.Dispose();
            }
            array = newArray;
        }


        [BurstCompile]
        public struct SetInstanceDataJob : IJobFor
        {
            [ReadOnly]
            public NativeArray<float4x4> LocalTransform;

            [ReadOnly]
            public NativeArray<byte> VisibleArray;

            [ReadOnly]
            public float4x4 ParentTransform;

            [ReadOnly]
            public int MaxInstancesPerWindow;
            [ReadOnly]
            public int WindowSizeInFloat4;

            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeArray<float4> SysMemBuffer;

            public void Execute(int index)
            {
                byte visible = VisibleArray[index];
                int objectToWorldOffset = BRGContainer.CalculateOffsets(index, 0, 3, MaxInstancesPerWindow, WindowSizeInFloat4);
                int worldToObjectOffset = BRGContainer.CalculateOffsets(index, 3, 3, MaxInstancesPerWindow, WindowSizeInFloat4);
                if (visible > 0)
                {
                    var objectToWorld = math.mul(ParentTransform, LocalTransform[index]);
                    var objectToWorldPac = BRGContainer.ConvertMatrix(objectToWorld);
                    var worldToobjectPac = BRGContainer.ConvertMatrix(math.inverse(objectToWorld));
                    SysMemBuffer[objectToWorldOffset + 0] = objectToWorldPac.Item1;
                    SysMemBuffer[objectToWorldOffset + 1] = objectToWorldPac.Item2;
                    SysMemBuffer[objectToWorldOffset + 2] = objectToWorldPac.Item3;
                    SysMemBuffer[worldToObjectOffset + 0] = worldToobjectPac.Item1;
                    SysMemBuffer[worldToObjectOffset + 1] = worldToobjectPac.Item2;
                    SysMemBuffer[worldToObjectOffset + 2] = worldToobjectPac.Item3;
                }
                else
                {
                    SysMemBuffer[objectToWorldOffset + 0] = 0;
                    SysMemBuffer[objectToWorldOffset + 1] = 0;
                    SysMemBuffer[objectToWorldOffset + 2] = 0;
                    SysMemBuffer[worldToObjectOffset + 0] = 0;
                    SysMemBuffer[worldToObjectOffset + 1] = 0;
                    SysMemBuffer[worldToObjectOffset + 2] = 0;
                }
            }
        }
    }
}
