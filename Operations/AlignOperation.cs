using System;
using System.Collections.Generic;
using System.Numerics;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Primitives.MapObjects;

namespace HammerTime.FaceTool.Operations
{
    public static class AlignOperation
    {
        public static Matrix4x4 CreateMatrix(Vector3 sourceNormal, Vector3 targetNormal, Vector3 center, bool lockPitch, bool lockRoll, bool lockYaw)
        {
            // We want sourceNormal to become -targetNormal
            var desiredNormal = -targetNormal;

            Quaternion finalRotation;

            int lockCount = (lockPitch ? 1 : 0) + (lockRoll ? 1 : 0) + (lockYaw ? 1 : 0);

            if (lockCount == 3)
            {
                // All axes locked — no rotation
                finalRotation = Quaternion.Identity;
            }
            else if (lockCount == 2)
            {
                // Only one rotation axis is allowed.
                // We project the vectors onto the plane perpendicular to the allowed axis.
                if (lockPitch && lockRoll) // Only Yaw (Z-axis rotation) allowed
                {
                    var v1 = new Vector2(sourceNormal.X, sourceNormal.Y);
                    var v2 = new Vector2(desiredNormal.X, desiredNormal.Y);
                    if (v1.LengthSquared() > 1e-6f && v2.LengthSquared() > 1e-6f)
                    {
                        v1 = Vector2.Normalize(v1);
                        v2 = Vector2.Normalize(v2);
                        float angle = (float)(Math.Atan2(v2.Y, v2.X) - Math.Atan2(v1.Y, v1.X));
                        finalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
                    }
                    else
                    {
                        finalRotation = Quaternion.Identity;
                    }
                }
                else if (lockPitch && lockYaw) // Only Roll (Y-axis rotation) allowed
                {
                    var v1 = new Vector2(sourceNormal.X, sourceNormal.Z);
                    var v2 = new Vector2(desiredNormal.X, desiredNormal.Z);
                    if (v1.LengthSquared() > 1e-6f && v2.LengthSquared() > 1e-6f)
                    {
                        v1 = Vector2.Normalize(v1);
                        v2 = Vector2.Normalize(v2);
                        float angle = (float)(Math.Atan2(v2.Y, v2.X) - Math.Atan2(v1.Y, v1.X));
                        finalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
                    }
                    else
                    {
                        finalRotation = Quaternion.Identity;
                    }
                }
                else // lockRoll && lockYaw -> Only Pitch (X-axis rotation) allowed
                {
                    var v1 = new Vector2(sourceNormal.Y, sourceNormal.Z);
                    var v2 = new Vector2(desiredNormal.Y, desiredNormal.Z);
                    if (v1.LengthSquared() > 1e-6f && v2.LengthSquared() > 1e-6f)
                    {
                        v1 = Vector2.Normalize(v1);
                        v2 = Vector2.Normalize(v2);
                        float angle = (float)(Math.Atan2(v2.Y, v2.X) - Math.Atan2(v1.Y, v1.X));
                        finalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle);
                    }
                    else
                    {
                        finalRotation = Quaternion.Identity;
                    }
                }
            }
            else
            {
                // For 0 or 1 locks, we calculate the full 3D rotation first.
                var cross = Vector3.Cross(sourceNormal, desiredNormal);
                float crossLen = cross.Length();

                Quaternion baseRotation;
                if (crossLen < 1e-6f)
                {
                    float dot = Vector3.Dot(sourceNormal, desiredNormal);
                    baseRotation = dot > 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(sourceNormal, Math.Abs(sourceNormal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY)), (float)Math.PI);
                }
                else
                {
                    var axis = Vector3.Normalize(cross);
                    float dot = Math.Clamp(Vector3.Dot(sourceNormal, desiredNormal), -1f, 1f);
                    baseRotation = Quaternion.CreateFromAxisAngle(axis, (float)Math.Acos(dot));
                }

                if (lockCount == 1)
                {
                    // If one axis is locked, we use swing-twist decomposition to remove the rotation around that axis.
                    // This is more stable than converting to Euler angles.
                    Quaternion rotation = baseRotation;
                    if (lockPitch)
                    {
                        rotation = rotation * Quaternion.Inverse(GetTwist(rotation, Vector3.UnitX));
                    }
                    if (lockRoll)
                    {
                        rotation = rotation * Quaternion.Inverse(GetTwist(rotation, Vector3.UnitY));
                    }
                    if (lockYaw)
                    {
                        rotation = rotation * Quaternion.Inverse(GetTwist(rotation, Vector3.UnitZ));
                    }
                    finalRotation = rotation;
                }
                else // lockCount == 0
                {
                    finalRotation = baseRotation;
                }
            }

            return Matrix4x4.CreateTranslation(-center) *
                   Matrix4x4.CreateFromQuaternion(finalRotation) *
                   Matrix4x4.CreateTranslation(center);
        }

        public static IOperation Create(IEnumerable<IMapObject> targets, Vector3 sourceNormal, Vector3 targetNormal, Vector3 center, bool lockPitch, bool lockRoll, bool lockYaw)
        {
            var matrix = CreateMatrix(sourceNormal, targetNormal, center, lockPitch, lockRoll, lockYaw);

            if (matrix == Matrix4x4.Identity)
                return new Sledge.BspEditor.Modification.Operations.Data.AddMapData();

            return new TransformWithTextures(matrix, targets);
        }

        /// <summary>
        /// Decomposes a quaternion into a "twist" component around a given axis.
        /// </summary>
        private static Quaternion GetTwist(Quaternion q, Vector3 axis)
        {
            axis = Vector3.Normalize(axis);
            var r = new Vector3(q.X, q.Y, q.Z);
            
            // Handle singularity: rotation by 180 degrees.
            // In this case, the "w" component is close to 0.
            if (q.W * q.W < 1e-6f) {
                // The rotation axis is specified by the vector part of the quaternion.
                // If this axis is aligned with the twist axis, the entire rotation is a twist.
                if (Vector3.Dot(Vector3.Normalize(r), axis) > 0.9999f) return q;
                // Otherwise, there is no twist component around this axis.
                return Quaternion.Identity;
            }

            var p = Project(r, axis);
            var twist = new Quaternion(p, q.W);
            return Quaternion.Normalize(twist);
        }
        
        /// <summary>
        /// Projects a vector onto another vector.
        /// </summary>
        private static Vector3 Project(Vector3 vector, Vector3 onNormal)
        {
            return Vector3.Dot(vector, onNormal) * onNormal;
        }


    }
}

