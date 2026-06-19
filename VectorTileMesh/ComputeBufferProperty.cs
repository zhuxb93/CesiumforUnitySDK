using System.Collections.Generic;
using UnityEngine;

namespace GeoTiles
{
    public class ComputeBufferProperty
    {
        //渲染的物体级材质
        private Mesh mesh;
        private Material[] MeshMaterial;

        //update每帧渲染的参数
        private ComputeBuffer[] argsBuffer;
        private ComputeBuffer meshPropertiesBuffer;

        //cesium Anchor
        private GameObject cesiumAnchor;

        //合并一张瓦片所有物体的包围盒
        private Bounds bounds;

        //这张瓦片渲染的物体数量
        public int instanceCount;

        //记录位置
        private Matrix4x4[] properties;
        private List<Vector3> Points;

        //ComputeBuffer渲染参数
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        /// <summary>
        /// 是否显示
        /// </summary>
        public bool IsVisible = false;
        public bool isDestory = false;

        private Color emissionColor = Color.black;

        private ComputeShader cullComputeShader;
        private ComputeBuffer cullResult;
        private int kernel;

        public ComputeBufferProperty(VegaMeshMaterialProperty meshProperty, List<Vector3> points, GameObject localToWorld, ComputeShader compute)
        {

            Points = points;
            instanceCount = points.Count;
            cesiumAnchor = localToWorld;
            emissionColor = meshProperty.nightColor;
            GraphicByType(meshProperty);

            args[1] = (uint)instanceCount;

            cullComputeShader = ComputeShader.Instantiate(compute);
            kernel = cullComputeShader.FindKernel("ViewPortCulling");
            cullResult = new ComputeBuffer(instanceCount, sizeof(float) * 16, ComputeBufferType.Append);

            argsBuffer = new ComputeBuffer[mesh.subMeshCount];
            for (int i = 0; i < argsBuffer.Length; i++)
            {
                argsBuffer[i] = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                args[0] = mesh.GetIndexCount(i);
                args[2] = mesh.GetIndexStart(i);
                args[3] = mesh.GetBaseVertex(i);
                argsBuffer[i].SetData(args);
            }

            properties = new Matrix4x4[instanceCount];

            for (int i = 0; i < properties.Length; i++)
            {
                Vector3 position = Points[i];
                var rotateY = Random.Range(0, 360);
                Quaternion rotation = Quaternion.Euler(0, rotateY, 0);

                //var scaleRandomXY = Random.Range(0.5f, 1.5f);
                //var scaleRandomZ = Random.Range(0.5f, 1.5f);

                //var scaleRandom = Random.Range(0.5f, 1.5f);
                //var scaleRandom = 2f;
                //var scaleRandomXY =0.01f;
                //var scaleRandomZ = 0.01f;
                //var scaleRandom = 20;
                properties[i] = Matrix4x4.TRS(position, rotation, new Vector3(2.5f, 1.5f, 2.5f));

                //if (level >= 14)
                //{
                //    var lonlat = PlottingTools.Instance.ConvertWorldPositionToLongitudeLatitudeHeight(position);
                //    GameObject go = GameObject.Instantiate(VectorVegetation.Instance.DebugTextMeshPro);
                //    go.GetComponent<TextMeshPro>().text = string.Format("{0},{1},{2}", lonlat.x, lonlat.y, id);
                //    go.transform.SetParent(localToWorld.transform, false);
                //    go.transform.localPosition = position + Vector3.up * 70;
                //    //go.transform.localScale = new Vector3(30, 30, 30);
                //}

            }

            //bounds = new Bounds(localToWorld.transform.localToWorldMatrix.MultiplyPoint(Vector3.zero), Vector3.zero);
            bounds = new Bounds(Vector3.zero, Vector3.zero);

            meshPropertiesBuffer = new ComputeBuffer(instanceCount, 16 * sizeof(float));
            meshPropertiesBuffer.SetData(properties);

            IsVisible = true;
        }

        /// <summary>
        /// 根据类型赋值
        /// </summary>
        /// <param name="vegaType"></param>
private void GraphicByType(VegaMeshMaterialProperty meshMaterial)
        {
            mesh = Mesh.Instantiate(meshMaterial.mesh);
            MeshMaterial = new Material[meshMaterial.materials.Length];
            for (int i = 0; i < MeshMaterial.Length; i++)
            {
                MeshMaterial[i] = Material.Instantiate(meshMaterial.materials[i]);
                MeshMaterial[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                MeshMaterial[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                MeshMaterial[i].SetInt("_ZWrite", 1);
                MeshMaterial[i].SetInt("_AlphaClip", 1);
                MeshMaterial[i].EnableKeyword("_ALPHATEST_ON");
                MeshMaterial[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                MeshMaterial[i].renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            //switch (vegaType)
            //{
            //    case VegetationType.Tree:
            //        Scale = Random.Range(10, 12);
            //        break;
            //    case VegetationType.Grass:
            //        Scale = Random.Range(20, 22);
            //        break;
            //    default:
            //        break;
            //}
        }




        public float alphaClipping = 1;

        /// <summary>
        /// 视锥剔除
        /// </summary>
        /// <param name="color"></param>
        /// <param name="planes"></param>
        public void UpdateBuffer(Vector4[] planes, bool isNight)
        {
            if (!IsVisible || MeshMaterial == null) return;

            if (!isDestory)
            {
                alphaClipping = alphaClipping > 0 ? alphaClipping - 0.01f : 0;
            }
            else
            {
                alphaClipping = alphaClipping < 1 ? alphaClipping + 0.01f : 1;

            }

            for (int i = 0; i < MeshMaterial.Length; i++)
            {
                cullComputeShader.SetBuffer(kernel, "cullInput", meshPropertiesBuffer);
                cullResult.SetCounterValue(0);
                cullComputeShader.SetBuffer(kernel, "cullresult", cullResult);
                cullComputeShader.SetInt("instanceCount", instanceCount);
                cullComputeShader.SetVectorArray("planes", planes);
                cullComputeShader.SetMatrix("_localToWorldMatrix", cesiumAnchor.transform.localToWorldMatrix);

                cullComputeShader.Dispatch(kernel, 1 + (instanceCount / 640), 1, 1);

                //MeshMaterial[i].SetBuffer("_positionBuffer", meshPropertiesBuffer);

                //因为在视锥剔除的ComputerBuffer中已经传入了本地矩阵，所有就不用在shader中再去乘了。
                //MeshMaterial[i].SetMatrix("_localToWorldMatrix", cesiumAnchor.transform.localToWorldMatrix);
                MeshMaterial[i].SetBuffer("_positionBuffer", cullResult);
                //MeshMaterial[i].SetColor("_BaseColor", color);
                MeshMaterial[i].SetFloat("_Cutoff", alphaClipping);

                MeshMaterial[i].SetColor("_EmissionColor", isNight ? emissionColor : Color.black);
                //获取实际要渲染的数量
                ComputeBuffer.CopyCount(cullResult, argsBuffer[i], sizeof(uint));

                Graphics.DrawMeshInstancedIndirect(mesh, 0, MeshMaterial[i], bounds, argsBuffer[i], 0, null, UnityEngine.Rendering.ShadowCastingMode.On, true, CesiumLoadingManager.Instance.CesiumLayer);
            }
        }


        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (meshPropertiesBuffer != null)
            {
                meshPropertiesBuffer.Release();
            }
            meshPropertiesBuffer = null;

            if (argsBuffer != null && argsBuffer.Length > 0)
            {
                for (int i = 0; i < argsBuffer.Length; i++)
                {
                    argsBuffer[i].Release();
                }
                argsBuffer = null;
            }
            if (cullResult != null)
            {
                cullResult.Release();
            }
            cullResult = null;

            if (cullComputeShader != null)
            {
                ComputeShader.Destroy(cullComputeShader);
            }
            cullComputeShader = null;

            if (MeshMaterial != null && MeshMaterial.Length > 0)
            {
                foreach (var material in MeshMaterial)
                {
                    Material.Destroy(material);
                }
                MeshMaterial = null;
            }

            if (mesh != null)
            {
                Mesh.Destroy(mesh);
            }
            mesh = null;
            if (cesiumAnchor != null)
            {
                GameObject.Destroy(cesiumAnchor);
            }
        }




        private Color GetRandomColor(Color startColor, Color endColor)
        {
            // 随机生成颜色的红、绿、蓝、透明度值
            float r = UnityEngine.Random.Range(startColor.r, endColor.r);
            float g = UnityEngine.Random.Range(startColor.g, endColor.g);
            float b = UnityEngine.Random.Range(startColor.b, endColor.b);

            // 返回随机生成的颜色
            return new Color(r, g, b);
        }



        public static class CullTool
        {
            //一个点和一个法向量确定一个平面
            public static Vector4 GetPlane(Vector3 normal, Vector3 point)
            {
                return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            }

            //三点确定一个平面
            public static Vector4 GetPlane(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                return GetPlane(normal, a);
            }

            //获取视锥体远平面的四个点
            public static Vector3[] GetCameraFarClipPlanePoint(Camera camera)
            {
                Vector3[] points = new Vector3[4];
                Transform transform = camera.transform;
                float distance = camera.farClipPlane;
                float halfFovRad = Mathf.Deg2Rad * camera.fieldOfView * 0.5f;
                float upLen = distance * Mathf.Tan(halfFovRad);
                float rightLen = upLen * camera.aspect;
                Vector3 farCenterPoint = transform.position + distance * transform.forward;
                Vector3 up = upLen * transform.up;
                Vector3 right = rightLen * transform.right;
                points[0] = farCenterPoint - up - right;//left-bottom
                points[1] = farCenterPoint - up + right;//right-bottom
                points[2] = farCenterPoint + up - right;//left-up
                points[3] = farCenterPoint + up + right;//right-up
                return points;
            }

            //获取视锥体的六个平面
            //public static Vector4[] GetFrustumPlane(Camera camera)
            //{
            //    Vector4[] planes = new Vector4[6];
            //    Transform transform = camera.transform;
            //    Vector3 cameraPosition = transform.position;
            //    Vector3[] points = GetCameraFarClipPlanePoint(camera);
            //    //顺时针
            //    planes[0] = GetPlane(cameraPosition, points[0], points[2]);//left
            //    planes[1] = GetPlane(cameraPosition, points[3], points[1]);//right
            //    planes[2] = GetPlane(cameraPosition, points[1], points[0]);//bottom
            //    planes[3] = GetPlane(cameraPosition, points[2], points[3]);//up
            //    planes[4] = GetPlane(-transform.forward, transform.position + transform.forward * camera.nearClipPlane);//near
            //    planes[5] = GetPlane(transform.forward, transform.position + transform.forward * camera.farClipPlane);//far
            //    return planes;
            //}

            public static Vector4[] GetFrustumPlane(Camera camera, float scale)
            {
                Vector4[] planes = new Vector4[6];
                Transform transform = camera.transform;
                Vector3 cameraPosition = transform.position;
                Vector3[] points = GetCameraFarClipPlanePoint(camera);

                // 计算扩展量
                Vector3 scaleOffset = scale * -transform.forward; // 可根据需要调整方向

                // 顺时针
                planes[0] = GetPlane(cameraPosition + scaleOffset, points[0] + scaleOffset, points[2] + scaleOffset); // left
                planes[1] = GetPlane(cameraPosition + scaleOffset, points[3] + scaleOffset, points[1] + scaleOffset); // right
                planes[2] = GetPlane(cameraPosition + scaleOffset, points[1] + scaleOffset, points[0] + scaleOffset); // bottom
                planes[3] = GetPlane(cameraPosition + scaleOffset, points[2] + scaleOffset, points[3] + scaleOffset); // up
                planes[4] = GetPlane(-transform.forward, transform.position + transform.forward * (camera.nearClipPlane - scale)); // near
                planes[5] = GetPlane(transform.forward, transform.position + transform.forward * (camera.farClipPlane + scale)); // far

                return planes;
            }
        }

    }

}