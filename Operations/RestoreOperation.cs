using System.Linq;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Primitives.MapObjects;

namespace HammerTime.FaceTool.Operations
{
    public static class RestoreOperation
    {
        /// <summary>
        /// Restores the exact objects that Rectify rotated, resolved by ID from the anchor.
        /// Current selection/scope is ignored on purpose — Restore must undo precisely what
        /// Rectify did, not whatever happens to be selected when the user clicks Restore.
        /// </summary>
        public static IOperation Create(MapDocument document)
        {
            var anchor = document.Map.Data.GetOne<TransformAnchor>();
            if (anchor == null)
            {
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();
            }

            var targets = anchor.AnchoredObjectIds
                .Select(id => document.Map.Root.FindByID(id))
                .Where(o => o != null)
                .ToList();

            if (!targets.Any())
            {
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();
            }

            return new TransformWithTextures(anchor.InverseTransform, targets);
        }
    }
}
