using CesiumForUnity;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace GeoTiles
{
    public class VectorPolylineChunk
    {
        private GameObject PolylineChunk;

        private MeshRenderer LineMeshRenderer;

        private MeshFilter LineMeshFilter;


        /// <summary>
        /// ID
        /// </summary>
        public string Name;


        /// <summary>
        /// 线的图层类型
        /// </summary>
        public VectorType LineLayerType;



        /// <summary>
        /// 瓦片ID
        /// </summary>
        public TileID TileIndex;

        /// <summary>
        /// 父节点
        /// </summary>
        public Transform ParentTransform;


        /// <summary>
        /// 道路透明动画
        /// </summary>
        private float To = 1f;
        private float Form = 0f;
        private Tween VectorTween;

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="parentTransform"></param>
        public VectorPolylineChunk(Transform parentTransform)
        {
            if (PolylineChunk == null)
            {
                PolylineChunk = new GameObject(Name);
                PolylineChunk.layer = CesiumLoadingManager.Instance.CesiumLayer;
            }
            if (LineMeshFilter == null)
            {
                LineMeshFilter = PolylineChunk.AddComponent<MeshFilter>();
            }
            if (LineMeshRenderer == null)
            {
                LineMeshRenderer = PolylineChunk.AddComponent<MeshRenderer>();
            }

            ParentTransform = parentTransform;
            PolylineChunk.transform.SetParent(ParentTransform);
        }




        public void RefreshVectorLineMesh(Mesh mesh, Material material, float offsetUp = 0)
        {
            LineMeshFilter.sharedMesh = mesh;
            LineMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            LineMeshRenderer.material = material;

            PolylineChunk.name = Name;
            PolylineChunk.transform.SetParent(ParentTransform);
            PolylineChunk.transform.localPosition = Vector3.up * offsetUp;
            PolylineChunk.transform.localRotation = Quaternion.identity;
            FromToAnimation(Form, To);
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

        /// <summary>
        /// 删除
        /// </summary>
        public void LocalRecycleModel()
        {
            if (LineMeshFilter.sharedMesh != null)
            {
                Mesh.Destroy(LineMeshFilter.sharedMesh);
            }

            if (LineMeshFilter != null)
            {
                MeshFilter.Destroy(LineMeshFilter);
            }
            if (LineMeshRenderer != null)
            {
                MeshRenderer.Destroy(LineMeshRenderer);
            }
            if (PolylineChunk != null)
            {
                GameObject.Destroy(PolylineChunk);
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
            if (PolylineChunk == null || LineMeshRenderer == null)
            {
                return;
            }

            LineMeshRenderer.material.SetFloat("_Alpha", from);
            VectorTween = DOVirtual.Float(from, to, time, (value) =>
            {
                if (LineMeshRenderer != null)
                {
                    LineMeshRenderer.material.SetFloat("_Alpha", value);
                }

            }).SetEase(easeType);
        }


    }
}

