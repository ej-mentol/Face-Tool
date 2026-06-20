using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Primitives.MapObjects;

namespace HammerTime.FaceTool.Operations
{
    public static class GridSnapOperation
    {
        public static IOperation Create(IEnumerable<IMapObject> targets, int gridSpacing)
        {
            if (gridSpacing <= 0 || !targets.Any()) 
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();

            var transaction = new Transaction();

            foreach (var target in targets)
            {
                var box = target.BoundingBox;
                if (box == null) continue;

                var center = box.Center;
                float spacing = gridSpacing;

                float snapX = (float)Math.Round(center.X / spacing) * spacing;
                float snapY = (float)Math.Round(center.Y / spacing) * spacing;
                float snapZ = (float)Math.Round(center.Z / spacing) * spacing;

                var offset = new Vector3(snapX - center.X, snapY - center.Y, snapZ - center.Z);

                if (offset.LengthSquared() < 0.0001f)
                {
                    continue;
                }

                var transform = Matrix4x4.CreateTranslation(offset);
                transaction.Add(new TransformWithTextures(transform, target));
            }

            return transaction;
        }
    }
}
