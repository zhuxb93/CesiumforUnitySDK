# CesiumforUnitySDK

[English](README.en.md)

CesiumforUnitySDK 是一份面向 Unity 2022.3 与 CesiumForUnity 的个人技术积累仓库，整理了三维地球场景中常见的矢量瓦片网格化、GPU 实例化、相机关键帧录播、软光栅烘焙和若干轻量工具。

这不是一个完整平台，也不包含任何真实业务服务、密钥、商业资产或私有数据。仓库的目标是把可复用的工程经验沉淀成相对独立的 Unity Package，方便学习、迁移和二次实现。

## 亮点导航

- `GpuInstancing/`：基于 `BatchRendererGroup` 的实例容器，封装 GPU buffer 上传、窗口对齐、Burst Job 填充和可见性裁剪委托。
- `VectorTileMesh/`：把建筑、道路、POI、文本等矢量瓦片数据转成 Unity Mesh，并按视锥动态维护可见瓦片。
- `Triangulation/Earcut/`：mapbox earcut 的 C# 移植，用于带洞多边形三角化。
- `CameraKeyframePlayer/`：运行时录制、编辑、播放相机关键帧，并导出 JSON 运镜数据。
- `Baking/SoftRasterizer/`：CPU 软光栅化工具，用重心插值把顶点色烘焙到纹理。
- `Utils/`：二进制序列化、轻量对象池、OBJ 导出等常用工程工具。

## 安装与依赖

1. 在 Unity Package Manager 中选择 `Add package from disk...`，指向本目录的 `package.json`。
2. 先安装并启用 CesiumForUnity，确保工程可以引用 `CesiumRuntime`。
3. 安装 `package.json` 中声明的 Unity 官方依赖：`mathematics`、`burst`、`collections`、`newtonsoft-json`。
4. 如需使用 Cesium ion，请在自己的项目配置中提供 token。示例只使用 `YOUR_CESIUM_ION_TOKEN` 占位，不内置任何密钥。

## 使用建议

先从 `Samples~/README.md` 里的合成数据说明开始，不要直接接入生产瓦片服务。`VectorTileMesh` 中的坐标转换和相机控制已经通过 `ICoordinateConverter`、`ICameraRig` 抽象，方便替换成你自己的 Cesium rig 或离线测试实现。

历史文件名 `InstanceContanier` 已统一更名为 `InstanceContainer`；如从旧代码迁移，需要同步更新引用。

## 脱敏与许可

- 仓库已移除私有品牌命名、内网地址、真实服务端点、密钥和业务数据。
- `LICENSE` 仅覆盖本人原创和改写部分。
- CesiumForUnity、earcut、Unity 官方包和 Newtonsoft Json 等第三方依赖按各自许可使用，详情见 `THIRD_PARTY_NOTICES.md`。
- 复核记录见 `脱敏复核报告.md`。

## 与其它仓库的关系

- 与 `CesiumforUnrealSDK` 对照：同一类矢量瓦片渲染问题在 Unity/C# 和 Unreal/C++ 中的两种实现。
- 与 `UnityGeoToolkit` 对照：Cesium 生态路线与编辑器导入框架、地形/路网/雷达工具链的工程取舍。
- 相机关键帧播放器可与 Unreal 版地球相机控制器互相参考。

## 当前状态

本仓已完成源码整理、中文模块说明、英文入口说明、第三方许可清单和脱敏复核。尚未在 Unity Editor 中完成真实导入编译，公开使用前建议先在 Unity 2022.3 工程中跑一轮本地包导入验证。
