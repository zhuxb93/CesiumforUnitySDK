using CesiumForUnity;
using DG.Tweening;
using UnityEngine;

namespace GeoTiles
{
    public class VectorPolygonChunk
    {
        private GameObject PolygonChunk;

        private MeshRenderer PolygonMeshRenderer;

        private MeshFilter PolygonMeshFilter;

        /// <summary>
        /// ID
        /// </summary>
        public string Name;

        /// <summary>
        /// 合并多边形ID集合
        /// </summary>
        //public List<string> CombineFeatureID;

        /// <summary>
        /// 矢量类型
        /// </summary>
        public VectorType PolygonLayerType;

        /// <summary>
        /// 
        /// </summary>
        public TileBuildingStatus SingleBuildingStatus = TileBuildingStatus.Automatic;

        /// <summary>
        /// 瓦片ID
        /// </summary>
        public TileID TileIndex;

        /// <summary>
        /// 父节点
        /// </summary>
        public Transform ParentTransform;

        /// <summary>
        /// 建筑生长动画
        /// </summary>
        private float To = 1f;
        private float Form = 0f;
        private Tween VectorTween;

        public VectorPolygonChunk(Transform parentTransform)
        {
            if (PolygonChunk == null)
            {
                PolygonChunk = new GameObject(Name);
                PolygonChunk.layer = CesiumLoadingManager.Instance.CesiumLayer;
            }

            if (PolygonMeshFilter == null)
            {
                PolygonMeshFilter = PolygonChunk.AddComponent<MeshFilter>();
            }
            if (PolygonMeshRenderer == null)
            {
                PolygonMeshRenderer = PolygonChunk.AddComponent<MeshRenderer>();
            }
            ParentTransform = parentTransform;
            PolygonChunk.transform.SetParent(ParentTransform);
        }

        public void RefreshVectorMesh(Mesh mesh, Material material, bool isCollider = false)
        {

            PolygonMeshFilter.sharedMesh = mesh;
            if (isCollider)
            {
                var meshCollider = PolygonChunk.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }
            if (PolygonLayerType == VectorType.buia)
            {
                PolygonMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            }

            PolygonMeshRenderer.material = material;

            PolygonChunk.name = Name;
            PolygonChunk.transform.SetParent(ParentTransform);
            //PolygonChunk.transform.localPosition = Vector3.zero + Vector3.up * To;
            PolygonChunk.transform.localPosition = Vector3.zero;
            PolygonChunk.transform.localRotation = Quaternion.identity;
            FromToAnimation(Form, To);

        }

        public Vector3 GetMeshCenter()
        {
            var meshVertex = PolygonMeshFilter.sharedMesh.vertices;

            return TileCalculator.GetPolygonCenter(meshVertex);
        }

        /// <summary>
        /// 矢量瓦片刷新时通知
        /// </summary>
        private void VectorRefreshAction()
        {
            //if (PolygonCenter == Vector3.zero) return;

            //var isOuterEnabled = Triangulator.IsPointInsidePolygon(PolygonCenter + ParentTransform.localPosition, VectorTileset.Instance.OuterCoordinateCircle);

            ////是否在内圈。如果再内圈则需要拉高
            //var isEnabled = Triangulator.IsPointInsidePolygon(PolygonCenter, VectorTileset.Instance.InnerCoordinateCircle);

            //if (isOuterEnabled && !PolygonMeshRenderer.enabled)
            //{
            //    PolygonMeshRenderer.enabled = true;
            //    FromToAnimation(-(Form + To), To);
            //}
            //else if (!isOuterEnabled && PolygonMeshRenderer.enabled)
            //{
            //    FromToAnimation(To, -(Form + To));
            //    VectorTween.onComplete = () =>
            //    {
            //        PolygonMeshRenderer.enabled = false;
            //    };
            //}

        }

        /// <summary>
        /// 回收至对象池
        /// </summary>
        public void RecycleModel()
        {
            FromToAnimation(To, Form);
            VectorTween.onComplete = () =>
            {
                LocalRecycleModel();
            };
        }

        ///// <summary>
        ///// 回收至对象池
        ///// </summary>
        //public void RecycleModel(float time, Ease easeType)
        //{
        //    //VectorTileset.Instance.VectorRefreshAction -= VectorRefreshAction;
        //    FromToAnimation(To, Form, time, easeType);
        //    VectorTween.onComplete = () =>
        //    {
        //        LocalRecycleModel();
        //    };
        //}



        public void LocalRecycleModel()
        {
            //VectorTileset.Instance.VectorRefreshAction -= VectorRefreshAction;

            if (PolygonMeshFilter.sharedMesh != null)
            {
                Mesh.Destroy(PolygonMeshFilter.sharedMesh);
            }

            if (PolygonMeshFilter != null)
            {
                MeshFilter.Destroy(PolygonMeshFilter);
            }
            if (PolygonMeshRenderer != null)
            {
                MeshRenderer.Destroy(PolygonMeshRenderer);
            }
            if (PolygonChunk != null)
            {
                GameObject.Destroy(PolygonChunk);
            }
            var Anchor = ParentTransform.GetComponent<CesiumGlobeAnchor>();
            if (Anchor != null)
            {
                GameObject.Destroy(Anchor);
            }
            if (ParentTransform != null)
            {
                GameObject.Destroy(ParentTransform.gameObject);
            }
        }



        public void FromToAnimation(float from, float to, float time = 1f, Ease easeType = Ease.Unset)
        {
            if (VectorTween != null)
            {
                VectorTween.Complete();
            }
            if (PolygonChunk == null)
            {
                return;
            }

            PolygonChunk.transform.localScale = new Vector3(1, from, 1);
            VectorTween = DOVirtual.Float(from, to, time, (value) =>
            {
                if (PolygonChunk != null)
                {
                    PolygonChunk.transform.localScale = new Vector3(1, value, 1);
                }

            }).SetEase(easeType);
        }



        public void SetPolygonChunkActive(bool isActive)
        {
            PolygonChunk.SetActive(isActive);
        }

        public bool GetPolygonChunkActive()
        {
            return PolygonChunk.activeSelf;
        }

        public void SetPolygonChunkLayer(int layer)
        {
            PolygonChunk.layer = layer;
        }

        public Material GetChunkMaterial()
        {
            return PolygonMeshRenderer.material;
        }

        ///// <summary>
        ///// 
        ///// </summary>
        //public void VisualDestory()
        //{
        //    FromToAnimation(MeshZ, -(Height + MeshZ), 1f, Ease.InQuad);
        //}

    }

}