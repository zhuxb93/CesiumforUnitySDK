using CesiumForUnity;
using DG.Tweening;
using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public class VectorTextChunk
    {
        public GameObject TextChunk;

        //private GameObject TextMeshProGameObj;
        private TextMeshPro TextMeshPro;
        private Material TextMeshProMaterial;
        //private TextMeshProEffect TextMeshProEffect;
        private Transform ScaleTransform;
        private Bounds rectBounds;
        private Renderer textRenderer;
        private SpriteRenderer[] spriteRenderers;
        public TileID tileID;

        public Transform ParentTransform;

        public string Text;
        public string Kind;
        public int Importance;
        public bool isVisible;
        public Rect textRect;
        public double cameraDistance = 0;

        public bool isRecycle = false;


        //private Material TextMaterial;

        public VectorTextChunk(string text, string kind, string importance, Sprite sp, Color color, Transform parentTransform, GameObject textPrefab)
        {
            ParentTransform = parentTransform;
            Text = text;
            Kind = kind;
            Importance = Convert.ToInt32(importance);

            TextChunk = GameObject.Instantiate(textPrefab, ParentTransform);
            //TextChunk.transform.SetParent(ParentTransform);
            TextChunk.name = Text;

            TextMeshPro = TextChunk.GetComponentInChildren<TextMeshPro>();
            TextMeshProMaterial = TextMeshPro.gameObject.GetComponent<MeshRenderer>().material;

            var icons = TextChunk.GetComponentsInChildren<SpriteRenderer>();
            foreach (var item in icons)
            {
                if (!item.name.Equals("Mask_02"))
                {
                    if (item.name.Equals("Icon"))
                    {
                        item.sprite = sp;
                    }
                    else
                    {
                        item.color = color;
                    }
                }
            }


            //TextMeshProGameObj = TextMeshPro.gameObject;
            //TextMeshProEffect = TextMeshProGameObj.GetComponent<TextMeshProEffect>();
            ScaleTransform = TextMeshPro.transform.parent;

            TextMeshPro.text = Text;

            rectBounds = ScaleTransform.GetComponent<BoxCollider>().bounds;
            textRenderer = TextMeshPro.GetComponent<MeshRenderer>();
            spriteRenderers = ScaleTransform.GetComponentsInChildren<SpriteRenderer>(true);
            SetVisible(false);
            isRecycle = false;
        }

        public VectorTextChunk(string text, string kind, string importance, Transform parentTransform, GameObject textPrefab)
        {
            ParentTransform = parentTransform;
            Text = text;
            Kind = kind;
            Importance = Convert.ToInt32(importance);

            TextChunk = GameObject.Instantiate(textPrefab, ParentTransform);
            //TextChunk.transform.SetParent(ParentTransform);
            TextChunk.name = Text;

            TextMeshPro = TextChunk.GetComponentInChildren<TextMeshPro>();
            TextMeshProMaterial = TextMeshPro.gameObject.GetComponent<MeshRenderer>().material;
            //TextMeshProGameObj = TextMeshPro.gameObject;
            //TextMeshProEffect = TextMeshProGameObj.GetComponent<TextMeshProEffect>();
            ScaleTransform = TextMeshPro.transform.parent;

            TextMeshPro.text = Text;

            rectBounds = ScaleTransform.GetComponent<BoxCollider>().bounds;
            textRenderer = TextMeshPro.GetComponent<MeshRenderer>();
            spriteRenderers = ScaleTransform.GetComponentsInChildren<SpriteRenderer>(true);
            SetVisible(false);
            isRecycle = false;
        }


        public void PlayAnimation()
        {
            FromToAnimation(0, 1, 200, 0);
        }

        public void RecycleModel()
        {
            isRecycle = true;
            if (TextChunk != null)
            {
                GameObject.Destroy(TextChunk);
                //FromToAnimation(1, 0, 0, 200);
                //VectorTween.onComplete = () =>
                //{
                //    GameObject.Destroy(TextChunk);
                //};

            }
        }

        public Transform GetTextMeshProTransform()
        {
            return ScaleTransform;
        }

        public TextMeshPro GetTextMeshPro()
        {
            return TextMeshPro;
        }

        public Bounds GetBounds()
        {
            rectBounds = ScaleTransform.GetComponent<BoxCollider>().bounds;
            return rectBounds;
        }

        public void SetVisible(bool isVisible)
        {
            textRenderer.enabled = isVisible;
            foreach (var item in spriteRenderers)
            {
                item.enabled = isVisible;
            }
            if (this.isVisible != isVisible)
            {
                if (isVisible)
                {
                    PlayAnimation();
                }
            }
            this.isVisible = isVisible;

        }

        //public bool GetVisbile(Camera camera)
        //{
        //    Bounds bounds = GetBounds();

        //    Vector3[] corners = new Vector3[8];
        //    corners[0] = bounds.min;
        //    corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
        //    corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
        //    corners[3] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
        //    corners[4] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
        //    corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        //    corners[6] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
        //    corners[7] = bounds.max;

        //    bool isVisible = true;
        //    foreach (Vector3 corner in corners)
        //    {
        //        Vector3 screenPoint = camera.WorldToScreenPoint(corner);
        //        if (screenPoint.x < 0 || screenPoint.x > Screen.width ||
        //            screenPoint.y < 0 || screenPoint.y > Screen.height ||
        //            screenPoint.z < 0)
        //        {
        //            isVisible = false;
        //            break;
        //        }
        //    }
        //    return isVisible;
        //}

/// <summary>
        /// 判断位置是否在摄像机范围内
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
public bool GetVisbile(Camera camera)
        {
            if (isRecycle)
            {
                return false;
            }
            Transform camTransform = camera.transform;
            Vector2 viewPos = camera.WorldToViewportPoint(TextChunk.transform.position);
            Vector3 dir = (TextChunk.transform.position - camTransform.position).normalized;
            float dot = Vector3.Dot(camTransform.forward, dir);     //判断物体是否在相机前面

            if (dot > 0 && viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1)
            {
                if (IsOccludedByEarth(camera))
                {
                    return false;
                }
                return true;
            }
            else
                return false;
        }

        public bool IsBehindEarth(Camera camera)
        {
            if (isRecycle)
            {
                return false;
            }
            return IsOccludedByEarth(camera);
        }

        private bool IsOccludedByEarth(Camera camera)
        {
            var cesiumGeoreference = CesiumLoadingManager.Instance.cesiumGeoreference;
            
            double3 cameraWorldPos = new double3(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z);
            double3 poiWorldPos = new double3(TextChunk.transform.position.x, TextChunk.transform.position.y, TextChunk.transform.position.z);
            
            double3 cameraECEF = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(cameraWorldPos);
            double3 poiECEF = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(poiWorldPos);
            
            double3 cameraDir = math.normalize(cameraECEF);
            double3 poiDir = math.normalize(poiECEF);
            double dotProduct = math.dot(cameraDir, poiDir);
            double angleBetween = math.acos(math.clamp(dotProduct, -1.0, 1.0));
            double angleDegrees = angleBetween * 180.0 / math.PI;
            
            if (angleDegrees > 90.0)
            {
                return true;
            }
            
            return false;
        }

        //public void SetColor(Color vertexColor, Color faceColor, Gradient gradient)
        //{
        //    if (VectorTween != null && VectorTween.IsPlaying())
        //    {
        //        VectorTween.onComplete = () =>
        //        {
        //            this.TextMeshPro.color = vertexColor;
        //            TextMeshProMaterial.SetColor("_FaceColor", faceColor);
        //            TextMeshProEffect.Gradient = gradient;
        //        };
        //    }
        //    else
        //    {
        //        this.TextMeshPro.color = vertexColor;
        //        TextMeshProMaterial.SetColor("_FaceColor", faceColor);
        //        TextMeshProEffect.Gradient = gradient;
        //    }


        //}

        public void SetAlpha(int alpha)
        {
            var FaceColor = TextMeshProMaterial.GetColor("_FaceColor");
            var OutlineColor = TextMeshProMaterial.GetColor("_OutlineColor");
            var UnderlayColor = TextMeshProMaterial.GetColor("_UnderlayColor");
            var GlowColor = TextMeshProMaterial.GetColor("_GlowColor");


            TextMeshProMaterial.SetColor("_FaceColor", new Color(FaceColor.r, FaceColor.g, FaceColor.b, alpha));
            TextMeshProMaterial.SetColor("_OutlineColor", new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, alpha));
            TextMeshProMaterial.SetColor("_UnderlayColor", new Color(UnderlayColor.r, UnderlayColor.g, UnderlayColor.b, alpha));
            TextMeshProMaterial.SetColor("_GlowColor", new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha));
        }

        /// <summary>
        /// 动画
        /// </summary>
        private Sequence VectorTween;


        public void FromToAnimation(float from, float to, float upForm, float upTo, float time = 0.5f)
        {
            if (VectorTween != null)
            {
                VectorTween.Complete();
            }
            VectorTween = DOTween.Sequence();

            var delayTime = UnityEngine.Random.Range(0, 0.3f);

            var FaceColor = TextMeshProMaterial.GetColor("_FaceColor");
            var OutlineColor = TextMeshProMaterial.GetColor("_OutlineColor");
            var UnderlayColor = TextMeshProMaterial.GetColor("_UnderlayColor");
            var GlowColor = TextMeshProMaterial.GetColor("_GlowColor");

            TextMeshProMaterial.SetColor("_FaceColor", new Color(FaceColor.r, FaceColor.g, FaceColor.b, from));
            TextMeshProMaterial.SetColor("_OutlineColor", new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, from));
            TextMeshProMaterial.SetColor("_UnderlayColor", new Color(UnderlayColor.r, UnderlayColor.g, UnderlayColor.b, from));
            TextMeshProMaterial.SetColor("_GlowColor", new Color(GlowColor.r, GlowColor.g, GlowColor.b, from));

            ScaleTransform.localPosition = Vector3.up * upForm;

            VectorTween.Append(DOVirtual.Float(upForm, upTo, time, (value) =>
            {
                if (TextChunk != null)
                {
                    ScaleTransform.localPosition = Vector3.up * value;
                }
            }).SetDelay(delayTime));

            VectorTween.Join(DOVirtual.Float(from, to, time, (value) =>
            {
                TextMeshProMaterial.SetColor("_FaceColor", new Color(FaceColor.r, FaceColor.g, FaceColor.b, value));
                TextMeshProMaterial.SetColor("_OutlineColor", new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, value));
                TextMeshProMaterial.SetColor("_UnderlayColor", new Color(UnderlayColor.r, UnderlayColor.g, UnderlayColor.b, value));
                TextMeshProMaterial.SetColor("_GlowColor", new Color(GlowColor.r, GlowColor.g, GlowColor.b, value));
            }).SetDelay(delayTime));


        }


        public void SetTextChunkActive(bool isActive)
        {
            if (TextChunk != null)
            {
                TextChunk.SetActive(isActive);
            }

        }

    }
}