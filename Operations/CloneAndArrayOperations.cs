using System.Collections.Generic;
using System.Linq;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Modification.Operations.Tree;
using Sledge.BspEditor.Primitives.MapObjects;

namespace HammerTime.FaceTool.Operations
{
    public static class CloneAndArrayOperations
    {
        public static IOperation CloneToFace(
            IEnumerable<IMapObject> sourceObjects,
            FaceTool.Tools.FaceTool tool,
            bool lockRotPitch, bool lockRotRoll, bool lockRotYaw,
            float offset)
        {
            if (tool.SourceFace == null || tool.TargetFace == null || !sourceObjects.Any())
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();

            var doc = tool.GetDocument();
            var transaction = new Transaction();

            // 1. Compute Align and Snap matrices once
            var alignMatrix = AlignOperation.CreateMatrix(
                tool.SourceFace.Plane.Normal,
                tool.TargetFace.Plane.Normal,
                tool.SourceFace.Origin,
                lockRotPitch, lockRotRoll, lockRotYaw);

            var rotatedVertices = tool.SourceFace.Vertices
                .Select(v => System.Numerics.Vector3.Transform(v, alignMatrix))
                .ToList();

            var snapMatrix = SnapOperation.CreateMatrix(
                rotatedVertices,
                tool.TargetFace.Plane,
                offset,
                false, false, false);

            foreach (var sourceObject in sourceObjects)
            {
                // 2. Clone using Copy with NumberGenerator so it gets new unique IDs
                var clone = (IMapObject)sourceObject.Copy(doc.Map.NumberGenerator);
                var parentId = sourceObject.Hierarchy.Parent.ID;

                // 3. Apply matrices directly to clone geometry
                TransformWithTextures.ApplyDirect(clone, alignMatrix);
                TransformWithTextures.ApplyDirect(clone, snapMatrix);

                // 4. Attach to the map tree
                transaction.Add(new Attach(parentId, clone));
            }

            return transaction;
        }
    }
}

