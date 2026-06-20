using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapData;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.Common.Transport;

namespace HammerTime.FaceTool.Operations
{
    [System.Serializable]
    public class TransformAnchor : IMapData
    {
        public Matrix4x4 InverseTransform { get; set; }

        /// <summary>
        /// IDs of the exact objects that were rotated by Rectify. Restore must
        /// target this set, not whatever is selected at the time of Restore —
        /// otherwise a changed selection rotates the wrong objects back.
        /// </summary>
        public List<long> AnchoredObjectIds { get; set; }

        public bool AffectsRendering => false;

        public TransformAnchor(Matrix4x4 inverseTransform, IEnumerable<long> anchoredObjectIds)
        {
            InverseTransform = inverseTransform;
            AnchoredObjectIds = anchoredObjectIds.ToList();
        }

        protected TransformAnchor(SerializationInfo info, StreamingContext context)
        {
            AnchoredObjectIds = new List<long>();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
        }

        public IMapElement Clone()
        {
            return new TransformAnchor(InverseTransform, AnchoredObjectIds);
        }

        public IMapElement Copy(UniqueNumberGenerator generator)
        {
            return Clone();
        }

        public SerialisedObject ToSerialisedObject()
        {
            // Transient in-memory data. Serialization is a formality;
            // anchor should not survive map saves.
            var so = new SerialisedObject("TransformAnchor");
            so.Set("M11", InverseTransform.M11); so.Set("M12", InverseTransform.M12);
            so.Set("M13", InverseTransform.M13); so.Set("M14", InverseTransform.M14);
            so.Set("M21", InverseTransform.M21); so.Set("M22", InverseTransform.M22);
            so.Set("M23", InverseTransform.M23); so.Set("M24", InverseTransform.M24);
            so.Set("M31", InverseTransform.M31); so.Set("M32", InverseTransform.M32);
            so.Set("M33", InverseTransform.M33); so.Set("M34", InverseTransform.M34);
            so.Set("M41", InverseTransform.M41); so.Set("M42", InverseTransform.M42);
            so.Set("M43", InverseTransform.M43); so.Set("M44", InverseTransform.M44);
            so.Set("AnchoredObjectIds", string.Join(",", AnchoredObjectIds));
            return so;
        }
    }
}
