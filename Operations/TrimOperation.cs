using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Modification.Operations.Tree;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace HammerTime.FaceTool.Operations
{
    public static class TrimOperation
    {
        public static IOperation Create(MapDocument document, Solid sourceSolid, Sledge.DataStructures.Geometric.Plane clipPlane, bool invertSide = false)
        {
            // No centroid heuristic: after Align+Snap the solid is intentionally
            // overlapping the wall, so BoundingBox.Center can sit on either side
            // depending on how deep the overlap is. "Front" (per clipPlane.Normal,
            // which comes from TargetFace.Plane) is the side facing away from the
            // wall by convention — invertSide is the only override, not geometry.
            bool keepFront = !invertSide;

            sourceSolid.Split(document.Map.NumberGenerator, clipPlane, out Solid back, out Solid front);

            var parentId = sourceSolid.Hierarchy.Parent.ID;
            var transaction = new Transaction();

            if (keepFront)
            {
                if (front != sourceSolid)
                {
                    transaction.Add(new Detatch(parentId, sourceSolid));
                    if (front != null)
                    {
                        transaction.Add(new Attach(parentId, front));
                    }
                }
            }
            else
            {
                if (back != sourceSolid)
                {
                    transaction.Add(new Detatch(parentId, sourceSolid));
                    if (back != null)
                    {
                        transaction.Add(new Attach(parentId, back));
                    }
                }
            }

            return transaction;
        }

        public static IOperation CreateTransformAndTrim(MapDocument document, IEnumerable<IMapObject> targets, Matrix4x4 matrix, Sledge.DataStructures.Geometric.Plane clipPlane, bool invertSide = false)
        {
            var transaction = new Transaction();
            foreach (var target in targets)
            {
                var solids = target.FindAll().OfType<Solid>().ToList();
                if (solids.Count > 0)
                {
                    foreach (var solid in solids)
                    {
                        var parentId = solid.Hierarchy.Parent.ID;
                        var clone = (Solid)solid.Copy(document.Map.NumberGenerator);
                        
                        TransformWithTextures.ApplyDirect(clone, matrix);

                        bool keepFront = !invertSide;
                        clone.Split(document.Map.NumberGenerator, clipPlane, out Solid back, out Solid front);

                        var kept = keepFront ? front : back;

                        if (kept != clone && kept != null)
                        {
                            transaction.Add(new Detatch(parentId, solid));
                            transaction.Add(new Attach(parentId, kept));
                        }
                        else
                        {
                            transaction.Add(new TransformWithTextures(matrix, solid));
                        }
                    }
                }
                else
                {
                    transaction.Add(new TransformWithTextures(matrix, target));
                }
            }
            return transaction;
        }
    }
}
