using Unity.Mathematics;

namespace GeoTiles
{
    public interface ICameraRig
    {
        double CameraToLookCenterDistance { get; }
        double SkewAngle { get; }
        double RotationAngle { get; }
        double3 LookCenterPoint { get; }
        void FlyTo(double3 ecefCenter, double distance, double rotation, double tilt, double duration = 1.0);
    }
}
