using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
            int sourceAnchorIndex,
            int targetAnchorIndex,
            bool lockRotPitch, bool lockRotRoll, bool lockRotYaw,
            float offset,
            bool lockX, bool lockY, bool lockZ)
        {
            if (tool.SourceFace == null || tool.TargetFace == null || !sourceObjects.Any())
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();

            var doc = tool.GetDocument();
            var transaction = new Transaction();

            var sourcePivot = FacePlacement.GetAlignPivot(tool.SourceFace, sourceAnchorIndex);

            var alignMatrix = AlignOperation.CreateMatrix(
                tool.SourceFace.Plane.Normal,
                tool.TargetFace.Plane.Normal,
                sourcePivot,
                lockRotPitch, lockRotRoll, lockRotYaw);

            var snapMatrix = FacePlacement.ComputeSnapMatrix(
                tool.SourceFace,
                tool.TargetFace,
                alignMatrix,
                sourceAnchorIndex,
                targetAnchorIndex,
                offset,
                lockX, lockY, lockZ);

            foreach (var sourceObject in sourceObjects)
            {
                var clone = (IMapObject)sourceObject.Copy(doc.Map.NumberGenerator);
                var parentId = sourceObject.Hierarchy.Parent.ID;

                TransformWithTextures.ApplyDirect(clone, alignMatrix);
                TransformWithTextures.ApplyDirect(clone, snapMatrix);

                transaction.Add(new Attach(parentId, clone));
            }

            return transaction;
        }

        public static IOperation CreateArray(
            IEnumerable<IMapObject> sourceObjects,
            FaceTool.Tools.FaceTool tool,
            int sourceAnchorIndex,
            int targetAnchorIndex,
            int countX, int countY,
            decimal spacingX, decimal spacingY,
            bool keepHierarchy,
            bool lockRotPitch, bool lockRotRoll, bool lockRotYaw,
            float offset,
            bool lockX, bool lockY, bool lockZ)
        {
            if (tool.SourceFace == null || tool.TargetFace == null || !sourceObjects.Any())
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();

            var doc = tool.GetDocument();
            var transaction = new Transaction();

            var normal = tool.TargetFace.Plane.Normal;
            var up = Math.Abs(normal.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
            var tangent = Vector3.Normalize(Vector3.Cross(normal, up));
            var bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent));

            var sourcePivot = FacePlacement.GetAlignPivot(tool.SourceFace, sourceAnchorIndex);
            var targetPivot = targetAnchorIndex == FacePlacement.AnchorOff
                ? tool.TargetFace.Origin
                : FacePlacement.CalculateAnchorOnFace(tool.TargetFace, targetAnchorIndex);

            var alignMatrix = AlignOperation.CreateMatrix(
                tool.SourceFace.Plane.Normal,
                tool.TargetFace.Plane.Normal,
                sourcePivot,
                lockRotPitch, lockRotRoll, lockRotYaw);

            var baseSnapMatrix = FacePlacement.ComputeSnapMatrix(
                tool.SourceFace,
                tool.TargetFace,
                alignMatrix,
                sourceAnchorIndex,
                targetAnchorIndex,
                offset,
                lockX, lockY, lockZ);

            for (int x = 0; x < countX; x++)
            {
                for (int y = 0; y < countY; y++)
                {
                    var arrayOffset = tangent * (float)spacingX * x + bitangent * (float)spacingY * y;
                    var snapMatrix = baseSnapMatrix * Matrix4x4.CreateTranslation(arrayOffset);

                    foreach (var sourceObject in sourceObjects)
                    {
                        var clone = (IMapObject)sourceObject.Copy(doc.Map.NumberGenerator);
                        var parentId = keepHierarchy ? sourceObject.Hierarchy.Parent.ID : doc.Map.Root.ID;

                        TransformWithTextures.ApplyDirect(clone, alignMatrix);
                        TransformWithTextures.ApplyDirect(clone, snapMatrix);

                        transaction.Add(new Attach(parentId, clone));
                    }
                }
            }

            return transaction;
        }
    }
}
