using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public interface ICoordinateConverter
    {
        double3 LongitudeLatitudeHeightToEcef(double longitude, double latitude, double height = 0);
        Vector3 EcefToWorld(double3 ecef);
        double3 WorldToLongitudeLatitudeHeight(Vector3 worldPosition);
    }
}
