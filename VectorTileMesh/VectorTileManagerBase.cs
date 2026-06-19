using CesiumForUnity;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public abstract class VectorTileManagerBase<TChunk> : MonoBehaviour where TChunk : class
    {
        protected CesiumGeoreference cesiumGeoreference;
        protected GeoTilesCesiumCameraController cameraController;
        protected Camera mainGeoCamera;
        protected VectorTileCacheManager cacheManager;

        protected ConcurrentDictionary<TileID, List<TChunk>> tileChunkCache;
        protected ConcurrentDictionary<TileID, VectorLoadStatus> tileStatusCache;
        protected ConcurrentDictionary<TileID, TileBuildingStatus> shieldTiles;

        protected HashSet<TileID> refreshTileSet;

        protected CancellationTokenSource cancellationTokenSource;
        protected bool isRunning;
        protected bool isStopped;

        [Header("Tile Management Settings")]
        public float tileRefreshInterval = 0.5f;
        public float vectorLoadRange = 1.0f;
        public int selectLevel;

        protected abstract VectorType VectorType { get; }
        protected abstract bool ShouldLoadBasedOnDistance { get; }
        protected abstract float DistanceVisibleStart { get; }
        protected abstract float DistanceVisibleEnd { get; }

        protected virtual void Awake()
        {
            cesiumGeoreference = CesiumLoadingManager.Instance.cesiumGeoreference;
            cameraController = CesiumLoadingManager.Instance.geoTilesCesiumCameraController;
            mainGeoCamera = CesiumLoadingManager.Instance.mainGeoCamera;
            cacheManager = CesiumLoadingManager.Instance.vectorTileCacheManager;

            tileChunkCache = new ConcurrentDictionary<TileID, List<TChunk>>();
            tileStatusCache = new ConcurrentDictionary<TileID, VectorLoadStatus>();
            shieldTiles = new ConcurrentDictionary<TileID, TileBuildingStatus>();
            refreshTileSet = new HashSet<TileID>();
        }

        protected virtual void Start()
        {
            if (cacheManager != null)
            {
                cacheManager.InitializeCache(VectorType);
            }
        }

        protected virtual void Update()
        {
            if (isStopped) return;

            var distance = cameraController.CameraToLookCenterDistance;
            var shouldLoad = ShouldLoadBasedOnDistance &&
                             distance < DistanceVisibleStart + CameraLerpVisual.Instance.MeanHeightOfTerrain &&
                             distance >= DistanceVisibleEnd;

            if (shouldLoad && !isRunning)
            {
                isRunning = true;
                InvokeRepeating(nameof(RefreshTiles), 0f, tileRefreshInterval);
            }
            else if (!shouldLoad && isRunning)
            {
                isRunning = false;
                CancelInvoke(nameof(RefreshTiles));
                if (tileChunkCache.Count > 0)
                {
                    ClearAllTiles();
                }
            }
        }

protected virtual void RefreshTiles()
        {
            if (!isRunning) return;

            refreshTileSet = CameraFrustumCalculator.GetTilesInFrustum(
                mainGeoCamera, 
                cameraController, 
                vectorLoadRange, 
                selectLevel
            );

            GenerateTilesAsync().Forget();
        }

protected virtual async UniTask GenerateTilesAsync()
        {
            if (refreshTileSet.Count == 0) return;

            cancellationTokenSource = new CancellationTokenSource();

            if (isRunning)
            {
                var sortedTiles = SortTilesByDistanceToCamera(refreshTileSet);
                
                foreach (var tile in sortedTiles)
                {
                    if (ShouldSkipTile(tile)) continue;

                    if (!tileStatusCache.ContainsKey(tile))
                    {
                        tileStatusCache.TryAdd(tile, VectorLoadStatus.Unload);
                    }
                    else if (tileStatusCache[tile] != VectorLoadStatus.Unload)
                    {
                        continue;
                    }

                    await LoadTileAsync(tile, cancellationTokenSource);
                }
            }

            RemoveTilesOutsideView();
        }

        protected virtual List<TileID> SortTilesByDistanceToCamera(HashSet<TileID> tiles)
        {
            var cameraPos = mainGeoCamera.transform.position;
            var tileDistanceList = new List<(TileID tile, float distance)>();
            
            foreach (var tile in tiles)
            {
                var tileCenter = TileCalculator.GetTileEcefCenter(tile);
                var tileWorldPos = cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(tileCenter);
                var distance = Vector3.SqrMagnitude((float3)tileWorldPos - (float3)cameraPos);
                tileDistanceList.Add((tile, distance));
            }
            
            tileDistanceList.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            return tileDistanceList.ConvertAll(item => item.tile);
        }

        protected virtual bool ShouldSkipTile(TileID tile)
        {
            if (shieldTiles.TryGetValue(tile, out var status) && status == TileBuildingStatus.Hidden)
            {
                return true;
            }
            return false;
        }

        protected abstract UniTask LoadTileAsync(TileID tileID, CancellationTokenSource cancellationToken);

        protected virtual string BuildTileUrl(TileID tileID, string baseUrl)
        {
            return baseUrl
                .Replace("{z}", tileID.z.ToString())
                .Replace("{x}", tileID.x.ToString())
                .Replace("{y}", tileID.invY().ToString());
        }

        protected virtual async UniTask<byte[]> GetTileDataAsync(TileID tileID, string url)
        {
            var tileIdStr = tileID.ToString();
            
            if (cacheManager.HasTileInCache(VectorType, tileIdStr))
            {
                return cacheManager.GetTileData(VectorType, tileIdStr);
            }

            try
            {
                var webRequest = UnityEngine.Networking.UnityWebRequest.Get(url);
                await webRequest.SendWebRequest();
                
                if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var data = webRequest.downloadHandler?.data;
                    
                    if (data != null && data.Length > 0)
                    {
                        await cacheManager.SaveTileDataAsync(VectorType, tileIdStr, data);
                    }

                    return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load tile {tileIdStr}: {e.Message}");
            }

            return null;
        }

        protected virtual Transform CreateCesiumGlobeAnchor(TileID tileIndex, Transform parent, MeshCreateInfo meshCreateInfo)
        {
            var tileParent = new GameObject
            {
                name = $"{tileIndex.x}/{tileIndex.invY()}/{tileIndex.z}"
            };
            
            tileParent.transform.SetParent(parent);
            
            var anchor = tileParent.AddComponent<CesiumGlobeAnchor>();
            anchor.adjustOrientationForGlobeWhenMoving = false;
            anchor.detectTransformChanges = false;
            anchor.positionGlobeFixed = meshCreateInfo.TileCenterEcef;
            anchor.rotationEastUpNorth = Quaternion.Euler(0, 0, 0);
            tileParent.layer = CesiumLoadingManager.Instance.CesiumLayer;
            
            return tileParent.transform;
        }

        protected virtual void RemoveTilesOutsideView()
        {
            var tilesToRemove = tileStatusCache.Keys
                .Where(t => !refreshTileSet.Contains(t))
                .ToList();

            foreach (var tile in tilesToRemove)
            {
                tileStatusCache[tile] = VectorLoadStatus.Destroy;
            }

            var destroyedTiles = tileStatusCache
                .Where(t => t.Value == VectorLoadStatus.Destroy)
                .ToList();

            foreach (var tile in destroyedTiles)
            {
                if (shieldTiles.TryGetValue(tile.Key, out var status) && 
                    status == TileBuildingStatus.AlwaysVisible)
                {
                    continue;
                }

                tileStatusCache.TryRemove(tile.Key, out _);
                RemoveTileChunks(tile.Key);
            }
        }

        protected virtual void RemoveTileChunks(TileID tileID)
        {
            if (!tileChunkCache.TryRemove(tileID, out var chunks)) return;

            foreach (var chunk in chunks)
            {
                RecycleChunk(chunk);
            }
        }

        protected abstract void RecycleChunk(TChunk chunk);

        protected virtual void RemoveAllTiles()
        {
            if (tileChunkCache == null) return;
            
            foreach (var tile in tileChunkCache.Keys.ToList())
            {
                if (shieldTiles != null && shieldTiles.TryGetValue(tile, out var status) && 
                    status == TileBuildingStatus.AlwaysVisible)
                {
                    continue;
                }

                RemoveTileChunks(tile);
            }
        }

        public virtual void ClearAllTiles()
        {
            cancellationTokenSource?.Cancel();
            refreshTileSet?.Clear();
            RemoveAllTiles();
            tileStatusCache?.Clear();
        }

        public virtual void StopOrRun(bool isRun)
        {
            if (isRun)
            {
                isStopped = false;
            }
            else
            {
                cancellationTokenSource?.Cancel();
                refreshTileSet?.Clear();
                RemoveAllTiles();
                tileStatusCache?.Clear();
                isRunning = false;
                isStopped = true;
            }
        }

        public void AddOrUpdateTileStatus(TileID tileID, TileBuildingStatus status)
        {
            shieldTiles[tileID] = status;
        }

        public void RemoveTileStatus(TileID tileID)
        {
            shieldTiles.TryRemove(tileID, out _);
        }

        public TileBuildingStatus GetTileStatus(TileID tileID)
        {
            return shieldTiles.TryGetValue(tileID, out var status) 
                ? status 
                : TileBuildingStatus.Automatic;
        }

        protected virtual void OnDisable() => StopOrRun(false);
        protected virtual void OnEnable() => StopOrRun(true);
    }
}