using CesiumForUnity;
using Cysharp.Threading.Tasks;
using SpaceGraphicsToolkit;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoTiles
{
    public class CesiumConversions : MonoBehaviour
    {
        static CesiumConversions _instance;
        public static CesiumConversions Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<CesiumConversions>(includeInactive: true);
                }
                return _instance;
            }
        }

        private CesiumGeoreference cesiumGeoreference;

        private void Awake()
        {
            cesiumGeoreference = CesiumLoadingManager.Instance.cesiumGeoreference;
        }


        // Update is called once per frame
        /// <summary>
        /// 经纬度 -> 世界坐标
        /// </summary>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <returns></returns>
        public Vector3 ConvertLongitudeLatitudeHeightToWorldPosition(double lon, double lat, double height = 0)
        {
            var worldPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(lon, lat, height));
            return (float3)cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(worldPos);
        }

        public Vector3 ConvertLongitudeLatitudeHeightToWorldPosition(double lon, double lat, ref double4x4 ecefToLocal)
        {
            var ecefPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(lon, lat, 0));

            var worldPos = math.mul(ecefToLocal, new double4(ecefPos.x, ecefPos.y, ecefPos.z, 1));
            return (float3)worldPos.xyz;
        }


        /// <summary>
        ///  经纬度 -> 世界坐标
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector3 ConvertLongitudeLatitudeHeightToWorldPosition(double2 pos)
        {
            var worldPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(pos.x, pos.y, 0));
            return (float3)cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(worldPos);
        }

        /// <summary>
        /// 经纬度 -> ECEF
        /// </summary>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <returns></returns>
        public double3 ConvertLongitudeLatitudeHeightToECEF(double lon, double lat)
        {
            var worldPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(lon, lat, 0));
            return worldPos;
        }

        public double3 ConvertLongitudeLatitudeHeightToECEF(double lon, double lat, double height)
        {
            var worldPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(lon, lat, height));
            return worldPos;
        }

        /// <summary>
        /// 经纬度 -> ECEF
        /// </summary>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <returns></returns>
        public double3 ConvertLongitudeLatitudeHeightToECEF(double3 ecef)
        {
            var worldPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(ecef);
            return worldPos;
        }

        /// <summary>
        /// ECEF -> 世界坐标
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector3 ConvertECEFToWorldPosition(Vector3 pos)
        {
            return (float3)cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(new double3(pos.x, pos.y, pos.z));
        }

        public Vector3 ConvertECEFToWorldPosition(double3 pos)
        {
            return (float3)cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(pos);
        }

        public Vector3 ConvertECEFToWorldPosition(double3 ecefPos, ref double4x4 ecefToLocal)
        {
            var worldPos = math.mul(ecefToLocal, new double4(ecefPos.x, ecefPos.y, ecefPos.z, 1));
            return (float3)worldPos.xyz;
        }

        /// <summary>
        /// 世界坐标 -> 经纬度
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        public double3 ConvertWorldPositionToLongitudeLatitudeHeight(Vector3 worldPos)
        {
            var fixedPosition = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(worldPos.x, worldPos.y, worldPos.z));
            var LongitudeLatitudeHeight = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(fixedPosition);
            return LongitudeLatitudeHeight;
        }

        /// <summary>
        /// 世界坐标 -> ECEF
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        public double3 ConvertWorldPositionToECEF(Vector3 worldPos)
        {
            var fixedPosition = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(worldPos.x, worldPos.y, worldPos.z));
            return fixedPosition;
        }



        public async UniTask<string> UnityWebRequestJsonString(string fileName)
        {
            UnityWebRequest request = UnityWebRequest.Get(UnityGetWebRequestURL(fileName));
            await request.SendWebRequest(); //读取数据
            if (request.result == UnityWebRequest.Result.Success)
            {
                while (true)
                {
                    if (request.downloadHandler.isDone) //是否读取完数据
                    {
                        return request.downloadHandler.text;
                    }

                }
            }

            return string.Empty;

        }

        public byte[] UnityWebRequestByte(string fileName)
        {
            UnityWebRequest request = UnityWebRequest.Get(UnityGetWebRequestURL(fileName));
            request.SendWebRequest(); //读取数据
            if (request.result == UnityWebRequest.Result.Success)
            {
                while (true)
                {
                    if (request.downloadHandler.isDone) //是否读取完数据
                    {
                        return request.downloadHandler.data;
                    }

                }
            }

            return null;
        }


        public string UnityGetWebRequestURL(string fileName)
        {
            string url;
            #region 分平台判断 StreamingAssets 路径
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            url = "file://" + Application.dataPath + "/StreamingAssets/" + fileName;
#elif UNITY_IPHONE
        url = "file://" + Application.dataPath + "/Raw/"+ fileName;
#elif UNITY_ANDROID
        url = "jar:file://" + Application.dataPath + "!/assets/"+ fileName;
#endif
            #endregion
            return url;
        }



        /// <summary>
        /// 创建渐变贴图
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        private Texture2D CreateTexture(int height = 256, int width = 1)
        {
            Texture2D gradientTexture = SgtHelper.CreateTempTexture2D("GradientTexture", width, height, TextureFormat.ARGB32);
            gradientTexture.wrapMode = TextureWrapMode.Repeat;

            var stepU = 1.0f / (height - 1);

            for (int x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var u = stepU * y;

                    WritePixel(gradientTexture, u, x, y, Color.black, SgtEase.Type.Circular, 1, 3);
                }
            }

            var by = gradientTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(@"C:\Users\Dell\Desktop\StyleFolder\InnerTexture.png", by);

            gradientTexture.Apply();

            return gradientTexture;

        }

        private void WritePixel(Texture2D texture2D, float u, int x, int y, Color baseColor, SgtEase.Type ease, float colorSharpness, float alphaSharpness)
        {
            var colorU = SgtHelper.Sharpness(u, colorSharpness); colorU = SgtEase.Evaluate(ease, colorU);
            var alphaU = SgtHelper.Sharpness(u, alphaSharpness); alphaU = SgtEase.Evaluate(ease, alphaU);
            var color = Color.Lerp(baseColor, Color.white, colorU);

            color.a = alphaU;

            texture2D.SetPixel(x, y, SgtHelper.Saturate(color));
        }
    }
}
