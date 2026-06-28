using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace HammerTime.FaceTool.Operations
{
    public static class SnapOperation
    {
        public static Matrix4x4 CreateMatrix(IEnumerable<Vector3> sourceVertices, Sledge.DataStructures.Geometric.Plane targetPlane, float offset, bool lockX, bool lockY, bool lockZ, bool altMode = false)
        {
            // Closest vertex = smallest signed distance along the normal that brings it to the plane.
            // We want the vertex that is furthest "into" the target (i.e. would clip first),
            // so we pick the minimum signed distance from the plane.
            // If altMode is active, we pick the maximum signed distance (furthest point) to align from the opposite side.
            var sorted = sourceVertices
                .OrderBy(v => Vector3.Dot(targetPlane.Normal, v) - targetPlane.DistanceFromOrigin);

            var closestPoint = altMode ? sorted.Last() : sorted.First();

            float distance = Vector3.Dot(targetPlane.Normal, closestPoint) - targetPlane.DistanceFromOrigin;

            var moveVector = -targetPlane.Normal * distance + targetPlane.Normal * offset;

            if (lockX) moveVector.X = 0;
            if (lockY) moveVector.Y = 0;
            if (lockZ) moveVector.Z = 0;

            return Matrix4x4.CreateTranslation(moveVector);
        }

        public static Matrix4x4 CreateMatrix(Vector3 sourcePoint, Sledge.DataStructures.Geometric.Plane targetPlane, float offset, bool lockX, bool lockY, bool lockZ, bool altMode = false)
        {
            return CreateMatrix(new[] { sourcePoint }, targetPlane, offset, lockX, lockY, lockZ, altMode);
        }

        public static IOperation Create(IEnumerable<IMapObject> targets, IEnumerable<Vector3> sourceVertices, Sledge.DataStructures.Geometric.Plane targetPlane, float offset, bool lockX, bool lockY, bool lockZ)
        {
            var transform = CreateMatrix(sourceVertices, targetPlane, offset, lockX, lockY, lockZ);
            return new TransformWithTextures(transform, targets);
        }

        public static IOperation Create(IEnumerable<IMapObject> targets, Vector3 sourcePoint, Sledge.DataStructures.Geometric.Plane targetPlane, float offset, bool lockX, bool lockY, bool lockZ)
        {
            var transform = CreateMatrix(sourcePoint, targetPlane, offset, lockX, lockY, lockZ);
            return new TransformWithTextures(transform, targets);
        }
    }
}
