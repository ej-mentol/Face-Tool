using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Modification.Operations.Data;
using Sledge.BspEditor.Primitives.MapObjects;

namespace HammerTime.FaceTool.Operations
{
    public static class RectifyOperation
    {
        public static IOperation Create(MapDocument document, IEnumerable<IMapObject> targets, Vector3 sourceNormal, Vector3 center)
        {
            // Find the closest world axis to sourceNormal
            var axes = new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
            Vector3 bestAxis = Vector3.UnitZ;
            float maxDot = -2f;

            foreach (var axis in axes)
            {
                float dot = Vector3.Dot(sourceNormal, axis);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestAxis = axis;
                }
            }

            // Calculate rotation to align sourceNormal to bestAxis
            var cross = Vector3.Cross(sourceNormal, bestAxis);
            float crossLen = cross.Length();

            Quaternion rotation;
            if (crossLen < 1e-6f)
            {
                if (maxDot > 0)
                    rotation = Quaternion.Identity; // Already aligned
                else
                {
                    // Flipped 180°: pick any perpendicular axis
                    Vector3 perp = Math.Abs(sourceNormal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
                    Vector3 rotAxis = Vector3.Normalize(Vector3.Cross(sourceNormal, perp));
                    rotation = Quaternion.CreateFromAxisAngle(rotAxis, (float)Math.PI);
                }
            }
            else
            {
                float dot = Math.Clamp(Vector3.Dot(sourceNormal, bestAxis), -1f, 1f);
                rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(cross), (float)Math.Acos(dot));
            }

            var transform = Matrix4x4.CreateTranslation(-center) *
                            Matrix4x4.CreateFromQuaternion(rotation) *
                            Matrix4x4.CreateTranslation(center);

            Matrix4x4.Invert(transform, out var inverseTransform);
            var targetList = targets.ToList();
            var anchor = new TransformAnchor(inverseTransform, targetList.Select(t => t.ID));

            var transaction = new Transaction();
            
            // Reversibly replace anchor
            var existingAnchor = document.Map.Data.GetOne<TransformAnchor>();
            if (existingAnchor != null)
            {
                transaction.Add(new RemoveMapData(existingAnchor));
            }
            transaction.Add(new AddMapData(anchor));
            transaction.Add(new TransformWithTextures(transform, targetList));

            return transaction;
        }
    }
}
