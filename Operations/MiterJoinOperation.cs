using System;
using System.Numerics;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Primitives.MapObjects;
using Face = Sledge.BspEditor.Primitives.MapObjectData.Face;
namespace HammerTime.FaceTool.Operations
{
    public static class MiterJoinOperation
    {
        public static IOperation Create(MapDocument document, Solid solidA, Face faceA, Solid solidB, Face faceB, bool invertSide = false)
        {
            var n1 = faceA.Plane.Normal;
            var n2 = faceB.Plane.Normal;
            var originalN2 = n2;

            var p1 = faceA.Origin;
            var p2 = faceB.Origin;

            Vector3 splitOrigin;
            var w0 = p1 - p2;
            float b = Vector3.Dot(n1, n2);
            float den = 1.0f - b * b;
            if (Math.Abs(den) < 0.0001f)
            {
                splitOrigin = (faceA.Origin + faceB.Origin) / 2f;
            }
            else
            {
                float d = Vector3.Dot(n1, w0);
                float e = Vector3.Dot(n2, w0);
                float t = (b * e - d) / den;
                float u = (e - b * d) / den;
                var pt1 = p1 + t * n1;
                var pt2 = p2 + u * n2;
                splitOrigin = (pt1 + pt2) / 2f;
            }

            var bisectN2 = n2;
            if (Vector3.Dot(n1, bisectN2) > 0)
            {
                bisectN2 = -bisectN2;
            }

            var diff = n1 - bisectN2;
            var splitNormal = diff.LengthSquared() < 0.0001f ? n1 : Vector3.Normalize(diff);
            var splitPlane = new Sledge.DataStructures.Geometric.Plane(splitNormal, Vector3.Dot(splitNormal, splitOrigin));

            // Determine which side of the plane to keep for each solid based on face normal direction
            bool invertA = Vector3.Dot(splitNormal, -n1) > 0;
            bool invertB = Vector3.Dot(splitNormal, -originalN2) > 0;

            if (invertSide)
            {
                invertA = !invertA;
                invertB = !invertB;
            }

            var transaction = new Transaction();

            var splitOpA = TrimOperation.Create(document, solidA, splitPlane, invertA);
            if (splitOpA != null) transaction.Add(splitOpA);

            var splitOpB = TrimOperation.Create(document, solidB, splitPlane, invertB);
            if (splitOpB != null) transaction.Add(splitOpB);

            return transaction;
        }
    }
}
