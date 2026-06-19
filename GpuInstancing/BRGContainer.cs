using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;


namespace GeoTiles
{
    /// <summary>
    /// 管理BatchRendererGroup实例数据的容器类，提供GPU数据上传和批次管理功能。
    /// </summary>
    public unsafe class BRGContainer
    {
        public delegate NativeArray<int> VisibilityProviderDelegate(int InstanceCount, BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext);
        public int InstanceByte => m_instanceSize; // 每个实例占用的字节数
        public int ElementByte => m_ElementByte;  // 每个float4占用的字节数
        public int TotalGpuBufferSize => m_totalGpuBufferSize; // 总的GPU缓冲区大小（字节)
        public int MaxInstancePerWindow => m_maxInstancePerWindow; // 每个窗口支持的最大实例数
        public int WindowCount => m_windowCount; // 总窗口数
        public int InstanceCount => m_instanceCount; // 当前实例数
        public bool Initialized => m_initialized; // 是否完成初始化
        public int AlignedGPUWindowSize => m_alignedGPUWindowSize; // GPU缓冲区对齐后的窗口大小
        public int WindowSizeInFloat4 => m_WindowSizeInFloat4; // 每个窗口占用的float4数量
        private bool UseConstantBuffer => BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer; // 是否使用ConstantBuffer优化
        private int m_ElementByte = UnsafeUtility.SizeOf(typeof(float4)); // 每个float4的字节大小
        public bool CastShadows { get; private set; } // 是否启用阴影投射

        // 内部状态字段
        private int m_maxInstances; // 最大实例数
        private int m_instanceCount; // 当前实例数
        private int m_alignedGPUWindowSize; // GPU缓冲区对齐后的窗口大小
        private int m_WindowSizeInFloat4; // 每个窗口占用的float4数量
        private int m_maxInstancePerWindow; // 每个窗口支持的最大实例数
        private int m_windowCount; // 总窗口数
        private int m_totalGpuBufferSize; // 总的GPU缓冲区大小（字节）
        private NativeArray<float4> m_sysmemBuffer; // 系统内存缓冲区
        private bool m_initialized; // 是否完成初始化
        private int m_instanceSize; // 单实例所需的总字节大小
        private BatchID[] m_batchIDs; // BatchID数组
        private BatchMaterialID m_materialID; // 关联材质的BatchMaterialID
        private BatchMeshID m_meshID; // 关联网格的BatchMeshID
        private BatchRendererGroup m_BatchRendererGroup; // BatchRendererGroup实例
        private GraphicsBuffer m_GPUPersistentInstanceData; // GPU持久化实例数据缓冲区
        private VisibilityProviderDelegate m_VisibilityProviderDelegate;

        // 属性描述列表，用于定义所有需要处理的BRG属性
        private PropertyDescription[] m_propertyDescriptions;

        /// <summary>
        /// 初始化BRG容器
        /// </summary>
        /// <param name="mesh">网格对象</param>
        /// <param name="mat">材质对象</param>
        /// <param name="maxInstances">最大实例数</param>
        /// <param name="propertyDescriptions">属性描述列表</param>
        /// <param name="castShadows">是否启用阴影投射</param>
        /// <returns>返回是否成功初始化</returns>
        public bool Init(Mesh mesh, Material mat, int maxInstances, PropertyDescription[] propertyDescriptions, bool castShadows = true, VisibilityProviderDelegate visibilityProvider = null, IntPtr userContex = default)
        {
            m_VisibilityProviderDelegate = visibilityProvider;
            m_BatchRendererGroup = new BatchRendererGroup(this.OnPerformCulling, userContex);
            m_propertyDescriptions = propertyDescriptions;

            // 计算单实例的字节大小
            int instanceSize = 0;
            foreach (var pd in m_propertyDescriptions)
            {
                instanceSize += (int)(pd.Float4Count * m_ElementByte);
            }
            m_instanceSize = instanceSize;

            m_instanceCount = 0;
            m_maxInstances = maxInstances;
            CastShadows = castShadows;

            if (UseConstantBuffer)
            {
                // 使用ConstantBuffer进行对齐和窗口计算
                m_alignedGPUWindowSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
                m_maxInstancePerWindow = m_alignedGPUWindowSize / instanceSize;
                m_windowCount = (m_maxInstances + m_maxInstancePerWindow - 1) / m_maxInstancePerWindow;
                m_totalGpuBufferSize = m_windowCount * m_alignedGPUWindowSize;
                m_GPUPersistentInstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Constant, m_totalGpuBufferSize / m_ElementByte, m_ElementByte);
            }
            else
            {
                // 不使用ConstantBuffer时的计算
                m_alignedGPUWindowSize = (m_maxInstances * instanceSize + (m_ElementByte - 1)) & (-m_ElementByte);
                m_maxInstancePerWindow = maxInstances;
                m_windowCount = 1;
                m_totalGpuBufferSize = m_windowCount * m_alignedGPUWindowSize;
                m_GPUPersistentInstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, m_totalGpuBufferSize / 4, 4);
            }
            m_WindowSizeInFloat4 = m_alignedGPUWindowSize / m_ElementByte;

            // 初始化系统内存缓冲区
            m_sysmemBuffer = new NativeArray<float4>(m_totalGpuBufferSize / m_ElementByte, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            // 创建Batch Metadata
            var batchMetadata = new NativeArray<MetadataValue>(m_propertyDescriptions.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            uint offsetByte = 0;
            for (int i = 0; i < m_propertyDescriptions.Length; i++)
            {
                var pd = m_propertyDescriptions[i];
                batchMetadata[i] = CreateMetadataValue(pd.NameID, offsetByte, pd.IsPerInstance);
                offsetByte += (pd.Float4Count * (uint)m_maxInstancePerWindow) * (uint)m_ElementByte;
            }

            // 为每个窗口创建Batch
            m_batchIDs = new BatchID[m_windowCount];
            for (int b = 0; b < m_windowCount; b++)
            {
                int offset = b * m_alignedGPUWindowSize;
                m_batchIDs[b] = m_BatchRendererGroup.AddBatch(batchMetadata, m_GPUPersistentInstanceData.bufferHandle, (uint)offset, UseConstantBuffer ? (uint)m_alignedGPUWindowSize : 0);
            }

            batchMetadata.Dispose();

            // 设置全局包围盒
            UnityEngine.Bounds bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1e10f, 1e10f, 1e10f));
            m_BatchRendererGroup.SetGlobalBounds(bounds);

            if (mesh) m_meshID = m_BatchRendererGroup.RegisterMesh(mesh);
            if (mat) m_materialID = m_BatchRendererGroup.RegisterMaterial(mat);

            m_initialized = true;
            return true;
        }

        /// <summary>
        /// 将实例数据上传到GPU。
        /// </summary>
        /// <param name="instanceCount">需要上传的实例数量</param>
        /// <returns>返回是否成功上传</returns>
        [BurstCompile]
        public bool UploadGpuData(int instanceCount)
        {
            if ((uint)instanceCount > (uint)m_maxInstances)
                return false;

            m_instanceCount = instanceCount;
            int completeWindows = m_instanceCount / m_maxInstancePerWindow;

            // 上传完整窗口的数据
            if (completeWindows > 0)
            {
                int sizeInFloat4 = (completeWindows * m_alignedGPUWindowSize) / m_ElementByte;
                m_GPUPersistentInstanceData.SetData(m_sysmemBuffer, 0, 0, sizeInFloat4);
            }

            // 上传最后一个不完整窗口的数据
            int lastBatchId = completeWindows;
            int itemInLastBatch = m_instanceCount - m_maxInstancePerWindow * completeWindows;

            if (itemInLastBatch > 0)
            {
                int windowOffsetInFloat4 = (lastBatchId * m_alignedGPUWindowSize) / m_ElementByte;

                uint offsetElement = 0;
                foreach (var pd in m_propertyDescriptions)
                {
                    int propertyOffsetInFloat4 = (int)offsetElement + windowOffsetInFloat4;
                    // 上传长度 = itemInLastBatch * 属性维度
                    m_GPUPersistentInstanceData.SetData(
                        m_sysmemBuffer,
                        propertyOffsetInFloat4,
                        propertyOffsetInFloat4,
                        itemInLastBatch * (int)pd.Float4Count
                    );
                    offsetElement += (pd.Float4Count * (uint)m_maxInstancePerWindow);
                }
            }

            return true;
        }

        /// <summary>
        /// 关闭并释放资源。
        /// </summary>
        public void Shutdown()
        {
            if (m_initialized)
            {
                // 移除所有的Batch
                for (uint b = 0; b < m_windowCount; b++)
                    m_BatchRendererGroup.RemoveBatch(m_batchIDs[b]);

                // 释放注册的资源
                m_BatchRendererGroup.UnregisterMaterial(m_materialID);
                m_BatchRendererGroup.UnregisterMesh(m_meshID);

                // 释放内存和GPU资源
                m_BatchRendererGroup.Dispose();
                m_GPUPersistentInstanceData.Dispose();
                m_sysmemBuffer.Dispose();
            }
        }

        /// <summary>
        /// 获取系统内存缓冲区的引用和相关配置。
        /// </summary>
        /// <returns>返回系统内存缓冲区的引用</returns>
        public NativeArray<float4> GetSysmemBuffer()
        {
            return m_sysmemBuffer;
        }

        /// <summary>
        /// 创建一个MetadataValue，用于描述Batch中的属性偏移和实例数据类型。
        /// </summary>
        /// <param name="nameID">属性的NameID</param>
        /// <param name="gpuOffset">属性在GPU缓冲区中的偏移量</param>
        /// <param name="isPerInstance">是否为每实例数据</param>
        /// <returns>返回MetadataValue实例</returns>
        static MetadataValue CreateMetadataValue(int nameID, uint gpuOffset, bool isPerInstance)
        {
            const uint kIsPerInstanceBit = 0x80000000; // 标志位，用于标识是否为Per-Instance属性
            return new MetadataValue
            {
                NameID = nameID,
                Value = gpuOffset | (isPerInstance ? kIsPerInstanceBit : 0),
            };
        }

        /// <summary>
        /// 分配指定类型和数量的内存。
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="count">分配数量</param>
        /// <returns>返回分配的指针</returns>
        public static T* Malloc<T>(uint count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<T>() * (int)count,
                UnsafeUtility.AlignOf<T>(),
                Allocator.TempJob
            );
        }

        /// <summary>
        /// 执行剔除操作的回调方法。
        /// </summary>
        /// <param name="rendererGroup">BatchRendererGroup实例</param>
        /// <param name="cullingContext">剔除上下文</param>
        /// <param name="cullingOutput">剔除结果</param>
        /// <param name="userContext">用户上下文指针</param>
        /// <returns>返回调度的JobHandle</returns>
        [BurstCompile]
        public JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            if (m_initialized)
            {
                BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();

                // 计算绘制命令的数量
                int drawCommandCount = (m_instanceCount + m_maxInstancePerWindow - 1) / m_maxInstancePerWindow;
                int maxInstancePerDrawCommand = m_maxInstancePerWindow;
                drawCommands.drawCommandCount = drawCommandCount;

                // 设置绘制范围
                drawCommands.drawRangeCount = 1;
                drawCommands.drawRanges = Malloc<BatchDrawRange>(1);
                drawCommands.drawRanges[0] = new BatchDrawRange
                {
                    drawCommandsBegin = 0,
                    drawCommandsCount = (uint)drawCommandCount,
                    filterSettings = new BatchFilterSettings
                    {
                        renderingLayerMask = 1,
                        layer = 0,
                        motionMode = MotionVectorGenerationMode.Camera,
                        shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        receiveShadows = false,
                        staticShadowCaster = false,
                        allDepthSorted = false
                    }
                };

                // 设置可见实例数据
                if (drawCommands.drawCommandCount > 0)
                {
                    if (m_VisibilityProviderDelegate != null)
                    {
                        drawCommands.visibleInstances = (int*)m_VisibilityProviderDelegate(m_instanceCount, rendererGroup, cullingContext, cullingOutput, userContext).GetUnsafePtr();
                    }
                    else
                    {
                        // as we don't need culling, the visibility int array buffer will always be {0,1,2,3,...} for each draw command
                        // so we just allocate maxInstancePerDrawCommand and fill it
                        int visibilityArraySize = maxInstancePerDrawCommand;
                        if (m_instanceCount < visibilityArraySize)
                            visibilityArraySize = m_instanceCount;

                        drawCommands.visibleInstances = Malloc<int>((uint)visibilityArraySize);

                        // As we don't need any frustum culling in our context, we fill the visibility array with {0,1,2,3,...}
                        for (int i = 0; i < visibilityArraySize; i++)
                            drawCommands.visibleInstances[i] = i;
                    }

                    // Allocate the BatchDrawCommand array (drawCommandCount entries)
                    // In SSBO mode, drawCommandCount will be just 1
                    // 设置绘制命令
                    drawCommands.drawCommands = Malloc<BatchDrawCommand>((uint)drawCommandCount);
                    int left = m_instanceCount;
                    for (int b = 0; b < drawCommandCount; b++)
                    {
                        int inBatchCount = left > maxInstancePerDrawCommand ? maxInstancePerDrawCommand : left;
                        drawCommands.drawCommands[b] = new BatchDrawCommand
                        {
                            visibleOffset = 0,    // all draw command is using the same {0,1,2,3...} visibility int array
                            visibleCount = (uint)inBatchCount,
                            batchID = m_batchIDs[b],
                            materialID = m_materialID,
                            meshID = m_meshID,
                            submeshIndex = 0,
                            splitVisibilityMask = 0xff,
                            flags = BatchDrawCommandFlags.None,
                            sortingPosition = 0
                        };
                        left -= inBatchCount;
                    }
                }

                cullingOutput.drawCommands[0] = drawCommands;
                drawCommands.instanceSortingPositions = null;
                drawCommands.instanceSortingPositionFloatCount = 0;
            }

            return new JobHandle();
        }

        /// <summary>
        /// 根据索引和元素参数计算偏移量。
        /// </summary>
        /// <param name="globalIndex">全局索引，用于定位实例。</param>
        /// <param name="elementOffsetInFloat4">元素在每个实例内的偏移量（单位：float4）。</param>
        /// <param name="elementFloat4Count">每个元素占用的 float4 数量。</param>
        /// <param name="maxInstancesPerWindow">每个窗口中最多包含的实例数量。</param>
        /// <param name="windowSizeInFloat4">每个窗口占用的 float4 数量。</param>
        /// <returns>计算出的偏移量（单位：float4）。</returns>
        public static int CalculateOffsets(int globalIndex, int elementOffsetInFloat4, int elementFloat4Count, int maxInstancesPerWindow, int windowSizeInFloat4)
        {
            // 计算窗口 ID 和当前索引在窗口中的局部索引
            int windowId = System.Math.DivRem(globalIndex, maxInstancesPerWindow, out var localIndex);

            // 计算当前窗口在内存布局中的起始偏移量
            int windowOffsetInFloat4 = windowId * windowSizeInFloat4;

            // 综合偏移量：窗口偏移 + 每实例的偏移 + 当前元素在实例中的偏移
            return windowOffsetInFloat4 + maxInstancesPerWindow * elementOffsetInFloat4 + localIndex * elementFloat4Count;
        }

        public static (float4, float4, float4) ConvertMatrix(float4x4 m)
        {
            return (new float4(m.c0.x, m.c0.y, m.c0.z, m.c1.x),
                    new float4(m.c1.y, m.c1.z, m.c2.x, m.c2.y),
                    new float4(m.c2.z, m.c3.x, m.c3.y, m.c3.z));
        }

        /// <summary>
        /// 描述BatchRendererGroup(BRG)中一个属性的信息。
        /// </summary>
        public struct PropertyDescription
        {
            public int NameID;         // 属性在Shader中的NameID
            public uint Float4Count;     // 属性在每个实例中所占用的float4数量，比如Matrix4x4是4个float4，color是1个float4
            public bool IsPerInstance; // 是否是针对每个实例的数据（即属性在不同实例间值不同）

            /// <summary>
            /// 初始化一个属性描述
            /// </summary>
            /// <param name="nameID">属性在Shader中的NameID</param>
            /// <param name="float4Count">属性在每个实例中所占用的float4数量</param>
            /// <param name="isPerInstance">是否是针对每个实例的数据</param>
            public PropertyDescription(int nameID, uint float4Count, bool isPerInstance = true)
            {
                NameID = nameID;
                Float4Count = float4Count;
                IsPerInstance = isPerInstance;
            }
        }
    }

}
