using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Primitives.MapObjectData;

namespace HammerTime.FaceTool.Operations
{
    public static class FacePlacement
    {
        /// <summary>Center rose button — legacy behaviour, no anchor override.</summary>
        public const int AnchorOff = -1;

        public static Vector3 GetAlignPivot(Face sourceFace, int sourceAnchorIndex)
        {
            if (sourceAnchorIndex == AnchorOff)
                return sourceFace.Origin;
            return CalculateAnchorOnFace(sourceFace, sourceAnchorIndex);
        }

        public static Matrix4x4 ComputeSnapMatrix(
            Face sourceFace,
            Face targetFace,
            int sourceAnchorIndex,
            int targetAnchorIndex,
            float offset,
            bool lockX, bool lockY, bool lockZ)
        {
            return ComputeSnapMatrix(sourceFace, targetFace, Matrix4x4.Identity, sourceAnchorIndex, targetAnchorIndex, offset, lockX, lockY, lockZ);
        }

        public static Matrix4x4 ComputeSnapMatrix(
            Face sourceFace,
            Face targetFace,
            Matrix4x4 alignMatrix,
            int sourceAnchorIndex,
            int targetAnchorIndex,
            float offset,
            bool lockX, bool lockY, bool lockZ)
        {
            if (sourceAnchorIndex == AnchorOff && targetAnchorIndex == AnchorOff)
            {
                var rotatedVertices = sourceFace.Vertices
                    .Select(v => Vector3.Transform(v, alignMatrix))
                    .ToList();
                return SnapOperation.CreateMatrix(rotatedVertices, targetFace.Plane, offset, lockX, lockY, lockZ);
            }

            var normal = targetFace.Plane.Normal;

            if (targetAnchorIndex != AnchorOff && sourceAnchorIndex != AnchorOff)
            {
                var sourceAnchor = CalculateAnchorOnFace(sourceFace, sourceAnchorIndex);
                var rotatedSource = Vector3.Transform(sourceAnchor, alignMatrix);
                var targetAnchor = CalculateAnchorOnFace(targetFace, targetAnchorIndex);
                return CreateTranslationMove(rotatedSource, targetAnchor, normal, offset, lockX, lockY, lockZ);
            }

            if (targetAnchorIndex != AnchorOff && sourceAnchorIndex == AnchorOff)
            {
                var rotatedVertices = sourceFace.Vertices
                    .Select(v => Vector3.Transform(v, alignMatrix))
                    .ToList();
                var normalSnap = SnapOperation.CreateMatrix(rotatedVertices, targetFace.Plane, offset, lockX, lockY, lockZ);

                var originAfter = Vector3.Transform(sourceFace.Origin, alignMatrix * normalSnap);
                var targetAnchor = CalculateAnchorOnFace(targetFace, targetAnchorIndex);
                var tangential = ProjectOntoPlane(targetAnchor - originAfter, normal);
                tangential = ApplyLocks(tangential, lockX, lockY, lockZ);
                return normalSnap * Matrix4x4.CreateTranslation(tangential);
            }

            // Source anchor set, target off — snap anchor to plane only.
            var sourcePoint = CalculateAnchorOnFace(sourceFace, sourceAnchorIndex);
            var rotatedPoint = Vector3.Transform(sourcePoint, alignMatrix);
            return SnapOperation.CreateMatrix(new[] { rotatedPoint }, targetFace.Plane, offset, lockX, lockY, lockZ);
        }

        public static bool UsesLegacySnap(int sourceAnchorIndex, int targetAnchorIndex)
            => sourceAnchorIndex == AnchorOff && targetAnchorIndex == AnchorOff;

        public static Vector3 CalculateAnchorOnFace(Face face, int anchorIndex)
        {
            var verts = face.Vertices.ToList();
            if (verts.Count == 0) return Vector3.Zero;

            var normal = face.Plane.Normal;
            var up = Math.Abs(normal.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
            var tangent = Vector3.Normalize(Vector3.Cross(normal, up));
            var bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent));

            var points = verts.Select(v => new Vector2(Vector3.Dot(v, tangent), Vector3.Dot(v, bitangent))).ToList();
            float minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            float midX = (minX + maxX) / 2f, midY = (minY + maxY) / 2f;

            float tx = midX, ty = midY;
            switch (anchorIndex)
            {
                // Top row (minY), X is mirrored
                case 0: tx = maxX; ty = minY; break; // Top-Left UI -> Top-Right coordinate
                case 1: tx = midX; ty = minY; break; // Top-Center
                case 2: tx = minX; ty = minY; break; // Top-Right UI -> Top-Left coordinate

                // Middle row, X is mirrored
                case 3: tx = maxX; ty = midY; break; // Middle-Left UI -> Middle-Right coordinate
                case 4: tx = midX; ty = midY; break; // Middle-Center
                case 5: tx = minX; ty = midY; break; // Middle-Right UI -> Middle-Left coordinate

                // Bottom row (maxY), X is mirrored
                case 6: tx = maxX; ty = maxY; break; // Bottom-Left UI -> Bottom-Right coordinate
                case 7: tx = midX; ty = maxY; break; // Bottom-Center
                case 8: tx = minX; ty = maxY; break; // Bottom-Right UI -> Bottom-Left coordinate
            }

            var origin2D = new Vector2(Vector3.Dot(face.Origin, tangent), Vector3.Dot(face.Origin, bitangent));
            var diff = new Vector2(tx, ty) - origin2D;
            return face.Origin + tangent * diff.X + bitangent * diff.Y;
        }

        private static Matrix4x4 CreateTranslationMove(
            Vector3 from, Vector3 to, Vector3 normal, float offset,
            bool lockX, bool lockY, bool lockZ)
        {
            var move = to - from + normal * offset;
            move = ApplyLocks(move, lockX, lockY, lockZ);
            return Matrix4x4.CreateTranslation(move);
        }

        private static Vector3 ProjectOntoPlane(Vector3 vector, Vector3 normal)
            => vector - normal * Vector3.Dot(vector, normal);

        private static Vector3 ApplyLocks(Vector3 move, bool lockX, bool lockY, bool lockZ)
        {
            if (lockX) move.X = 0;
            if (lockY) move.Y = 0;
            if (lockZ) move.Z = 0;
            return move;
        }
    }
}
