# CesiumforUnitySDK

[中文](README.zh-CN.md)

CesiumforUnitySDK is a personal technical archive for Unity 2022.3 and CesiumForUnity. It extracts reusable work around vector-tile mesh generation, GPU instancing, camera keyframe playback, CPU rasterization, and a few small production utilities for 3D globe applications.

This repository is not a full product. It does not include private service endpoints, credentials, commercial assets, or business data. The purpose is to keep the reusable engineering ideas in a clean Unity Package that can be studied, ported, or reimplemented.

## Highlights

- `GpuInstancing/`: a `BatchRendererGroup` based instance container with GPU buffer upload, alignment handling, Burst Job filling, and visibility-culling hooks.
- `VectorTileMesh/`: a pipeline that converts building, road, POI, and text vector-tile data into Unity meshes and updates visible tiles from the camera frustum.
- `Triangulation/Earcut/`: a C# port of mapbox earcut for triangulating polygons with holes.
- `CameraKeyframePlayer/`: runtime camera keyframe recording, editing, playback, and JSON export.
- `Baking/SoftRasterizer/`: a CPU rasterizer that bakes vertex colors into textures with barycentric interpolation.
- `Utils/`: binary serialization, a lightweight object pool, OBJ export, and other small tools.

## Installation And Dependencies

1. In Unity Package Manager, choose `Add package from disk...` and select this repository's `package.json`.
2. Install and enable CesiumForUnity first, and make sure the project can reference `CesiumRuntime`.
3. Install the Unity package dependencies listed in `package.json`: `mathematics`, `burst`, `collections`, and `newtonsoft-json`.
4. If you use Cesium ion, provide your token through your own project configuration. This repository only uses the placeholder `YOUR_CESIUM_ION_TOKEN` and does not include credentials.

## Usage Notes

Start with the synthetic-data notes in `Samples~/README.md` instead of connecting production tile services directly. Coordinate conversion and camera control in `VectorTileMesh` are separated behind `ICoordinateConverter` and `ICameraRig`, so you can plug in your own Cesium rig or an offline test implementation.

The historical filename `InstanceContanier` has been normalized to `InstanceContainer`; update old references when migrating code.

## Sanitization And Licensing

- Private brand names, internal URLs, real service endpoints, credentials, and business data have been removed.
- `LICENSE` only covers original or rewritten code in this repository.
- CesiumForUnity, earcut, Unity packages, and Newtonsoft Json remain governed by their own licenses. See `THIRD_PARTY_NOTICES.md`.
- See `脱敏复核报告.md` for the sanitization review.

## Related Repositories

- Compare with `CesiumforUnrealSDK` for Unity/C# and Unreal/C++ implementations of similar vector-tile rendering problems.
- Compare with `UnityGeoToolkit` for Cesium-based workflows versus a custom Unity3DTiles path.
- The camera keyframe player can be read alongside the Unreal globe camera controller.

## Current Status

The source layout, Chinese module notes, English entry document, third-party notices, and sanitization review are complete. The package has not yet been imported and compiled in Unity Editor; run a Unity 2022.3 local package import before production use.
