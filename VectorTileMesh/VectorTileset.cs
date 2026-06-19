using CesiumForUnity;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public class VectorTileset : VectorTileManagerBase<object>
    {
        [Header("Vector Service Settings")]
        public string URL;

        [Header("Vector Layer Settings")]
        public SelectVectorLayer SelectVector;

        [Header("Distance Visibility")]
        public float distanceVisibleStart = 5000f;
        public float distanceVisibleEnd = 0f;

        [Header("Building Grid Settings")]
        public bool enableGridDraw = false;
        public int gridCellNum = 32;
        public int gridRowNum = 16;
        public float gridHeight = 25.0f;

        private ConcurrentDictionary<TileID, List<VectorPolygonChunk>> polygonTileCache;
        private ConcurrentDictionary<TileID, List<VectorPolylineChunk>> polylineTileCache;

        protected override VectorType VectorType => SelectVector.vectorType;
        protected override bool ShouldLoadBasedOnDistance => true;
        protected override float DistanceVisibleStart => distanceVisibleStart;
        protected override float DistanceVisibleEnd => distanceVisibleEnd;

        protected override void Awake()
        {
            base.Awake();
            
            polygonTileCache = new ConcurrentDictionary<TileID, List<VectorPolygonChunk>>();
            polylineTileCache = new ConcurrentDictionary<TileID, List<VectorPolylineChunk>>();
        }

        private void Start()
        {
            CreateVectorLayer();
        }

        private void CreateVectorLayer()
        {
            var vectorLayer = new GameObject($"{SelectVector.vectorType}Layer");
            vectorLayer.transform.SetParent(transform);
            vectorLayer.layer = CesiumLoadingManager.Instance.CesiumLayer;
            SelectVector.ParentTransform = vectorLayer;
        }

        protected override async UniTask LoadTileAsync(TileID tileID, CancellationTokenSource cancellationToken)
        {
            tileStatusCache[tileID] = VectorLoadStatus.Loading;

            if (IsTileAlreadyLoaded(tileID))
            {
                return;
            }

            var url = BuildTileUrl(tileID, URL);
            var tileIdStr = tileID.ToString();

            if (cancellationToken != null)
            {
                cancellationToken.Token.ThrowIfCancellationRequested();
            }

            var vectorData = await GetTileDataAsync(tileID, url);

            if (vectorData == null || vectorData.Length == 0)
            {
                tileStatusCache[tileID] = VectorLoadStatus.NoData;
                return;
            }

            tileStatusCache[tileID] = VectorLoadStatus.Loading;

            var meshCreateInfo = new MeshCreateInfo
            {
                TileCenterUnity = Vector3.zero,
                ecefToLocalMatrix = cesiumGeoreference.ecefToLocalMatrix,
                localToEcefMatrix = cesiumGeoreference.localToEcefMatrix,
                TileCenterEcef = double3.zero
            };

            if (SelectVector.vectorType == VectorType.buia)
            {
                await ProcessBuildingTile(vectorData, meshCreateInfo, tileID, cancellationToken);
            }
            else if (SelectVector.vectorType == VectorType.road || SelectVector.vectorType == VectorType.lrrl)
            {
                await ProcessRoadTile(vectorData, meshCreateInfo, tileID, cancellationToken);
            }

            tileStatusCache[tileID] = VectorLoadStatus.Loaded;
        }

        private bool IsTileAlreadyLoaded(TileID tileID)
        {
            return (SelectVector.vectorType == VectorType.buia && polygonTileCache.ContainsKey(tileID)) ||
                   (SelectVector.vectorType == VectorType.road && polylineTileCache.ContainsKey(tileID)) ||
                   (SelectVector.vectorType == VectorType.lrrl && polylineTileCache.ContainsKey(tileID));
        }

        private async UniTask ProcessBuildingTile(byte[] vectorData, MeshCreateInfo meshCreateInfo, TileID tileID, CancellationTokenSource cancellationToken)
        {
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            
            VectorTileParser.BuildingResult buildingResult;
            
            if (enableGridDraw)
            {
                buildingResult = await Task.Run(() =>
                {
                    if (cancellationToken != null)
                    {
                        cancellationToken.Token.ThrowIfCancellationRequested();
                    }
                    return VectorTileParser.ParseBuildingToGrid(vectorData, meshDataArray, selectLevel, gridCellNum, gridRowNum, gridHeight);
                }, cancellationToken.Token);
            }
            else
            {
                buildingResult = await Task.Run(() =>
                {
                    if (cancellationToken != null)
                    {
                        cancellationToken.Token.ThrowIfCancellationRequested();
                    }
                    return VectorTileParser.ParseBuilding(vectorData, meshDataArray, true);
                }, cancellationToken.Token);
            }

            if (!buildingResult.IsScucess)
            {
                return;
            }

            meshCreateInfo.TileCenterEcef = buildingResult.TileBasePoint;
            meshCreateInfo.TileCenterUnity = CesiumConversions.Instance.ConvertECEFToWorldPosition(
                meshCreateInfo.TileCenterEcef, 
                ref meshCreateInfo.ecefToLocalMatrix
            );

            CreateBuildingMesh(buildingResult, meshCreateInfo, tileID);
        }

        private void CreateBuildingMesh(VectorTileParser.BuildingResult buildingResult, MeshCreateInfo meshCreateInfo, TileID tileID)
        {
            if (!isRunning || polygonTileCache.ContainsKey(tileID))
            {
                return;
            }

            var polygonParent = CreateCesiumGlobeAnchor(tileID, SelectVector.ParentTransform.transform, meshCreateInfo);
            
            var combineMesh = new Mesh();
            Mesh.ApplyAndDisposeWritableMeshData(buildingResult.MeshDataArray, combineMesh);
            combineMesh.RecalculateNormals();
            combineMesh.RecalculateBounds();

            var chunk = new VectorPolygonChunk(polygonParent)
            {
                Name = "CombinePolygon",
                PolygonLayerType = VectorType.buia,
                TileIndex = tileID,
                ParentTransform = polygonParent.transform
            };

            chunk.RefreshVectorMesh(combineMesh, SelectVector.material);

            if (!polygonTileCache.ContainsKey(tileID))
            {
                polygonTileCache.TryAdd(tileID, new List<VectorPolygonChunk>());
            }

            polygonTileCache[tileID].Add(chunk);
            tileChunkCache[tileID] = new List<object> { chunk };
        }

        private async UniTask ProcessRoadTile(byte[] vectorData, MeshCreateInfo meshCreateInfo, TileID tileID, CancellationTokenSource cancellationToken)
        {
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            
            var roadResult = await Task.Run(() =>
            {
                if (cancellationToken != null)
                {
                    cancellationToken.Token.ThrowIfCancellationRequested();
                }
                return VectorTileParser.ParseRoad(vectorData, meshDataArray);
            }, cancellationToken.Token);

            meshCreateInfo.TileCenterEcef = roadResult.TileBasePoint;
            meshCreateInfo.TileCenterUnity = CesiumConversions.Instance.ConvertECEFToWorldPosition(
                meshCreateInfo.TileCenterEcef, 
                ref meshCreateInfo.ecefToLocalMatrix
            );

            CreateRoadMesh(roadResult, meshCreateInfo, tileID);
        }

        private void CreateRoadMesh(VectorTileParser.RoadResult roadResult, MeshCreateInfo meshCreateInfo, TileID tileID)
        {
            if (!isRunning || polylineTileCache.ContainsKey(tileID))
            {
                return;
            }

            var polylineParent = CreateCesiumGlobeAnchor(tileID, SelectVector.ParentTransform.transform, meshCreateInfo);
            
            var combineMesh = new Mesh();
            Mesh.ApplyAndDisposeWritableMeshData(roadResult.MeshDataArray, combineMesh);
            combineMesh.RecalculateNormals();
            combineMesh.RecalculateBounds();

            var chunk = new VectorPolylineChunk(polylineParent)
            {
                Name = "CombinePolyline",
                LineLayerType = VectorType.road,
                TileIndex = tileID,
                ParentTransform = polylineParent
            };

            chunk.RefreshVectorLineMesh(combineMesh, SelectVector.material, 10);

            if (!polylineTileCache.ContainsKey(tileID))
            {
                polylineTileCache.TryAdd(tileID, new List<VectorPolylineChunk>());
            }

            polylineTileCache[tileID].Add(chunk);
            tileChunkCache[tileID] = new List<object> { chunk };
        }

        protected override void RemoveTileChunks(TileID tileID)
        {
            if (SelectVector.vectorType == VectorType.buia)
            {
                if (polygonTileCache.TryRemove(tileID, out var polygonChunks))
                {
                    foreach (var chunk in polygonChunks)
                    {
                        chunk.LocalRecycleModel();
                    }
                }
            }
            else
            {
                if (polylineTileCache.TryRemove(tileID, out var polylineChunks))
                {
                    foreach (var chunk in polylineChunks)
                    {
                        chunk.LocalRecycleModel();
                    }
                }
            }

            base.RemoveTileChunks(tileID);
        }

        protected override void RecycleChunk(object chunk)
        {
            if (chunk is VectorPolygonChunk polygonChunk)
            {
                polygonChunk.LocalRecycleModel();
            }
            else if (chunk is VectorPolylineChunk polylineChunk)
            {
                polylineChunk.LocalRecycleModel();
            }
        }

        protected override void RemoveAllTiles()
        {
            if (polygonTileCache != null)
            {
                foreach (var tile in polygonTileCache.Keys)
                {
                    if (shieldTiles != null && shieldTiles.TryGetValue(tile, out var status) && status == TileBuildingStatus.AlwaysVisible)
                    {
                        continue;
                    }

                    if (polygonTileCache.TryRemove(tile, out var chunks))
                    {
                        foreach (var chunk in chunks)
                        {
                            chunk.LocalRecycleModel();
                        }
                    }
                }
            }

            if (polylineTileCache != null)
            {
                foreach (var tile in polylineTileCache.Keys)
                {
                    if (shieldTiles != null && shieldTiles.TryGetValue(tile, out var status) && status == TileBuildingStatus.AlwaysVisible)
                    {
                        continue;
                    }

                    if (polylineTileCache.TryRemove(tile, out var chunks))
                    {
                        foreach (var chunk in chunks)
                        {
                            chunk.LocalRecycleModel();
                        }
                    }
                }
            }

            base.RemoveAllTiles();
        }

        public async UniTask<List<VectorPolygonChunk>> SingleBuilding(TileID tileID)
        {
            var tileIdStr = tileID.ToString();
            var vectorData = cacheManager.GetTileData(VectorType, tileIdStr);

            if (vectorData == null || vectorData.Length == 0)
            {
                return null;
            }

            var meshCreateInfo = new MeshCreateInfo
            {
                TileCenterUnity = Vector3.zero,
                ecefToLocalMatrix = cesiumGeoreference.ecefToLocalMatrix,
                localToEcefMatrix = cesiumGeoreference.localToEcefMatrix,
                TileCenterEcef = double3.zero
            };

            var buildingCount = VectorTileParser.GetBuildingCount(vectorData);
            var meshDataArray = Mesh.AllocateWritableMeshData(buildingCount);
            
            var buildingResult = await Task.Run(() => VectorTileParser.ParseBuilding(vectorData, meshDataArray, false));

            meshCreateInfo.TileCenterEcef = buildingResult.TileBasePoint;
            meshCreateInfo.TileCenterUnity = CesiumConversions.Instance.ConvertECEFToWorldPosition(
                meshCreateInfo.TileCenterEcef, 
                ref meshCreateInfo.ecefToLocalMatrix
            );

            var polygonParent = CreateCesiumGlobeAnchor(tileID, SelectVector.ParentTransform.transform, meshCreateInfo);
            var meshes = new Mesh[buildingResult.MeshDataArray.Length];
            
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i] = new Mesh();
            }
            
            Mesh.ApplyAndDisposeWritableMeshData(buildingResult.MeshDataArray, meshes);

            var singleModelLayer = LayerMask.NameToLayer("SingleBuilding");
            var singleChunkArray = new List<VectorPolygonChunk>();

            for (int i = 0; i < meshes.Length; i++)
            {
                var meshItem = meshes[i];
                meshItem.RecalculateNormals();
                meshItem.RecalculateBounds();
                
                var chunk = new VectorPolygonChunk(polygonParent)
                {
                    Name = buildingResult.BuildingIDs[i].ToString(),
                    PolygonLayerType = VectorType.buia,
                    TileIndex = tileID,
                    ParentTransform = polygonParent.transform
                };

                chunk.RefreshVectorMesh(meshItem, SelectVector.material, true);
                chunk.SetPolygonChunkLayer(singleModelLayer);
                singleChunkArray.Add(chunk);
            }

            return singleChunkArray;
        }

        public bool ExistsBuilding(TileID tileID)
        {
            return (polygonTileCache != null && polygonTileCache.ContainsKey(tileID)) || 
                   (shieldTiles != null && shieldTiles.ContainsKey(tileID));
        }
    }
}