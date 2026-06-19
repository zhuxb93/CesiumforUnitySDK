# CesiumforUnitySDK

这是一个面向 Unity 2022.3 与 CesiumForUnity 的个人技术积累仓库，重点整理矢量瓦片 Mesh 管线、BatchRendererGroup 实例化、相机关键帧录播、软光栅烘焙和若干轻量工具。

## 亮点导航
- GpuInstancing：基于 BatchRendererGroup 的实例容器，封装 GPU buffer 上传、窗口对齐、Job 填充和可见性裁剪委托。
- VectorTileMesh：建筑、道路、POI、文本等矢量瓦片到 Unity Mesh 的分块生成管线。
- CameraKeyframePlayer：运行时录制、编辑、播放相机关键帧并导出 JSON。
- Baking/SoftRasterizer：CPU 软光栅化，把顶点色按重心插值烘焙到纹理。
- Utils：二进制序列化、对象池、OBJ 导出等通用工具。

## 构建与运行
1. 在 Unity Package Manager 中以本地包方式添加本目录。
2. 先安装 CesiumForUnity，并确认工程中可引用 CesiumRuntime。
3. 如需使用线上地形或影像服务，请自行配置 Cesium ion，并将示例中的 YOUR_CESIUM_ION_TOKEN 替换为自己的配置来源。

## 设计笔记
这批代码原本服务于三维地球上的矢量瓦片显示。提取后把项目命名空间改为 GeoTiles，并补了 ICoordinateConverter / ICameraRig 这类接口，便于把 Cesium 坐标转换和相机控制从业务代码里拆开。InstanceContanier 的历史拼写也统一改为 InstanceContainer。

## 与其它仓库的关系
- 矢量瓦片渲染可与 CesiumforUnrealSDK 的 C++ 实现对照阅读。
- 相机关键帧录播可与 CesiumforUnrealSDK 的地球相机控制器互相参考。
- 3D Tiles 路线和 UnityGeoToolkit 的自研 Unity3DTiles 路线形成对比。

## 致谢
CesiumForUnity 来自 CesiumGS，按 Apache-2.0 使用。Earcut 算法来自 mapbox earcut，按 ISC 使用。
