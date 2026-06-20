using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Primitives.MapObjectData;
using Sledge.BspEditor.Primitives.MapObjects;

namespace HammerTime.FaceTool.Operations
{
    /// <summary>
    /// Like Sledge's built-in Transform, but also applies TransformUniform
    /// to every Face's Texture so textures move with the brush.
    /// Scoped to FaceTool only — does not affect the global editor behavior.
    /// </summary>
    public class TransformWithTextures : IOperation
    {
        private readonly List<long> _idsToTransform;
        private readonly Matrix4x4 _matrix;

        public bool Trivial => false;

        public TransformWithTextures(Matrix4x4 matrix, params IMapObject[] objectsToTransform)
        {
            _matrix = matrix;
            _idsToTransform = objectsToTransform.Select(x => x.ID).ToList();
        }

        public TransformWithTextures(Matrix4x4 matrix, IEnumerable<IMapObject> objectsToTransform)
        {
            _matrix = matrix;
            _idsToTransform = objectsToTransform.Select(x => x.ID).ToList();
        }

        public Task<Change> Perform(MapDocument document)
        {
            var ch = new Change(document);
            var objects = _idsToTransform.Select(x => document.Map.Root.FindByID(x)).Where(x => x != null).ToList();

            foreach (var o in objects)
            {
                // Standard vertex transform (calls Face.Transform → moves vertices only)
                o.Transform(_matrix);

                // Additionally transform texture axes on all faces so textures follow geometry
                TransformTextures(o, _matrix);

                ch.UpdateRange(o.FindAll());
            }

            return Task.FromResult(ch);
        }

        public Task<Change> Reverse(MapDocument document)
        {
            if (!Matrix4x4.Invert(_matrix, out var inv))
                throw new Exception("Unable to reverse this operation.");

            var ch = new Change(document);
            var objects = _idsToTransform.Select(x => document.Map.Root.FindByID(x)).Where(x => x != null).ToList();

            foreach (var o in objects)
            {
                o.Transform(inv);
                TransformTextures(o, inv);
                ch.UpdateRange(o.FindAll());
            }

            return Task.FromResult(ch);
        }

        /// <summary>
        /// Applies TransformUniform to all Face textures on the object and its descendants.
        /// </summary>
        private static void TransformTextures(IMapObject obj, Matrix4x4 matrix)
        {
            // Direct faces on this object (if it's a Solid)
            foreach (var face in obj.Data.Get<Face>())
            {
                face.Texture?.TransformUniform(matrix);
            }

            // Recurse into children (e.g. Group containing Solids)
            foreach (var child in obj.Hierarchy)
            {
                TransformTextures(child, matrix);
            }
        }

        /// <summary>
        /// Helper: apply transform + texture transform directly to an object's geometry
        /// without going through the operation/ID system. Useful for clones before Attach.
        /// </summary>
        public static void ApplyDirect(IMapObject obj, Matrix4x4 matrix)
        {
            obj.Transform(matrix);
            TransformTextures(obj, matrix);
        }
    }
}
