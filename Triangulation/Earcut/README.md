# Earcut 三角化

## 解决什么问题
矢量瓦片建筑面和 GeoJSON 多边形经常带洞，需要稳定三角化。

## 实现思路
保留 earcut 的 z-order 加速思路，把外环和洞展平后输出三角形索引。

## 基本用法
调用 EarcutLibrarySDK.Tessellate 生成索引，再交给 MeshBuilder。

## 依赖
第三方 earcut，ISC 许可。
