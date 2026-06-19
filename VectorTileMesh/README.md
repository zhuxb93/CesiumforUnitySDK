# VectorTileMesh

## 解决什么问题
把瓦片数据转为可流式加载的 Unity Mesh，并控制可见瓦片范围。

## 实现思路
以 TileCalculator 计算瓦片层级和行列号，用对象池复用 chunk，Job 负责构建顶点和索引。

## 基本用法
接入 ICoordinateConverter 后创建 VectorTileset，按相机视锥更新可见瓦片。

## 依赖
CesiumForUnity 可选/推荐；坐标转换通过 ICoordinateConverter 抽象。
