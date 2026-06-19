using CesiumForUnity;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;
using static CesiumForUnity.GeoTilesCesiumCameraController;

namespace GeoTiles
{
    public class CameraLerpVisual : MonoBehaviour
    {
        public static CameraLerpVisual Instance;

       

        private GeoTilesCesiumCameraController cameraControl;
        private Camera mainGeoCamera;
        private double cameraToLookCenterDistance;


        public Action<bool> TileidVisibleAction;
        [HideInInspector]
        public bool TileidVisible = false;

        public float MeanHeightOfTerrain { get; set; }
        public bool isOpenMeanHeightOfTerrain { get; set; } = true;

        [Header("到一定距离切换相机远平面")]
        public float VectorDistances;
        private VectorSenceState SenceState = VectorSenceState.Earth;

        private void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            cameraControl = this.gameObject.GetComponent<GeoTilesCesiumCameraController>();
            mainGeoCamera = this.gameObject.GetComponent<Camera>();
            cameraToLookCenterDistance = cameraControl.CameraToLookCenterDistance;
        }

        void Update()
        {
            cameraToLookCenterDistance = cameraControl.CameraToLookCenterDistance;

          
            if (Input.GetKeyDown(KeyCode.T))
            {
                cameraControl.CancelAnimation(AnimationType.All);
                cameraControl.FlyTo(new double3(-2177901.42232555, 4388984.60350609, 4069713.4744473), 10482596.4338464, -8, 10);
            }

            if (cameraControl.CameraToLookCenterDistance < VectorDistances && !SenceState.Equals(VectorSenceState.Vector))
            {
                NoneAnimatorSwitchVector();
            }

            if (cameraControl.CameraToLookCenterDistance > VectorDistances && !SenceState.Equals(VectorSenceState.Earth))
            {
                NoneAnimatorSwitchEarth();
            }

            if (isOpenMeanHeightOfTerrain)
            {
                var isGrounded = Physics.Raycast(gameObject.transform.position, gameObject.transform.forward, out RaycastHit hit, Mathf.Infinity, 1 << CesiumLoadingManager.Instance.CesiumLayer);

#if UNITY_EDITOR
                Debug.DrawRay(gameObject.transform.position, gameObject.transform.forward, isGrounded ? Color.green : Color.red);
#endif

                if (isGrounded && hit.collider.transform.parent != null)
                {
                    var tileInfo = hit.collider.transform.parent.GetComponent<TileInfo>();
                    if (tileInfo == null || tileInfo.TileID.Equals(lastTileID))
                    {
                        return;
                    }
                    uniTaskToken?.Cancel();
                    uniTaskToken = new CancellationTokenSource();
                    GetTileHeight(tileInfo.TileID).Forget();
                    lastTileID = tileInfo.TileID;
                }
            }
        }

        private string HeightURL = @"https://example.com/service-endpoint";
        private string MedianURL = @"https://example.com/service-endpoint";
        private TileID lastTileID = TileID.Create();
        private CancellationTokenSource uniTaskToken;
        private float heightFactor = 500;

        private async UniTask GetTileHeight(TileID tileid)
        {
            if (tileid.z > 14 || tileid.z < 8) return;
            string url = MedianURL.Replace("{z}", tileid.z.ToString()).Replace("{x}", tileid.x.ToString()).Replace("{y}", tileid.y > 0 ? tileid.invY().ToString() : "0");
            if (uniTaskToken != null)
            {
                uniTaskToken.Token.ThrowIfCancellationRequested();
            }
            byte[] vectorPbf = (await UnityWebRequest.Get(url).SendWebRequest()).downloadHandler.data;
            if (vectorPbf != null && vectorPbf.Length > 0)
            {
                var heightStr = Encoding.Default.GetString(vectorPbf);
                var medianHeight = Convert.ToDouble(heightStr);
                if (Mathf.Abs((float)medianHeight - MeanHeightOfTerrain) < heightFactor)
                {
                    return;
                }

                MeanHeightOfTerrain = (float)medianHeight;

#if UNITY_EDITOR
                print(MeanHeightOfTerrain);
#endif
            }
        }

        public async UniTask<float> GetTileHeightValue(TileID tileid)
        {
            if (tileid.z > 14) return 0;
            string url = HeightURL.Replace("{z}", tileid.z.ToString()).Replace("{x}", tileid.x.ToString()).Replace("{y}", tileid.y > 0 ? tileid.invY().ToString() : "0");
            if (uniTaskToken != null)
            {
                uniTaskToken.Token.ThrowIfCancellationRequested();
            }
            byte[] vectorPbf = (await UnityWebRequest.Get(url).SendWebRequest()).downloadHandler.data;
            if (vectorPbf != null && vectorPbf.Length > 0)
            {
                var heightStr = Encoding.Default.GetString(vectorPbf);
                JObject obj = JObject.Parse(heightStr);
                var minHeight = Convert.ToDouble(obj["minHeight"]);
                var heightValue = Mathf.Round((float)minHeight / heightFactor) * heightFactor;
                return Mathf.Clamp(heightValue, 0, heightValue);
            }

            return 0;
        }


        public void NoneAnimatorSwitchVector()
        {
            SenceState = VectorSenceState.Vector;
            mainGeoCamera.farClipPlane = 2000000;
        }

        public void NoneAnimatorSwitchEarth()
        {
            SenceState = VectorSenceState.Earth;
            mainGeoCamera.farClipPlane = 3.538876e+08f;
        }

        public enum VectorSenceState
        {
            Vector,
            Earth
        }
    }
}