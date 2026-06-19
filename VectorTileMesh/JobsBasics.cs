using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public class MeshCreateInfo
    {
        public Vector3 TileCenterUnity;
        public double3 TileCenterEcef;
        public double4x4 localToEcefMatrix;
        public double4x4 ecefToLocalMatrix;
    }

}