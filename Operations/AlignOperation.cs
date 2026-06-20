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
                // 0 or 1 axes locked — use standard 3D rotation and filter using Euler angles
                var cross = Vector3.Cross(sourceNormal, desiredNormal);
                float crossLen = cross.Length();

                Quaternion baseRotation;
                if (crossLen < 1e-6f)
                {
                    float dot = Vector3.Dot(sourceNormal, desiredNormal);
                    if (dot > 0)
                    {
                        baseRotation = Quaternion.Identity;
                    }
                    else
                    {
                        Vector3 perp = Math.Abs(sourceNormal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
                        Vector3 axis = Vector3.Normalize(Vector3.Cross(sourceNormal, perp));
                        baseRotation = Quaternion.CreateFromAxisAngle(axis, (float)Math.PI);
                    }
                }
                else
                {
                    var axis = Vector3.Normalize(cross);
                    float dot = Math.Clamp(Vector3.Dot(sourceNormal, desiredNormal), -1f, 1f);
                    baseRotation = Quaternion.CreateFromAxisAngle(axis, (float)Math.Acos(dot));
                }

                if (lockPitch || lockRoll || lockYaw)
                {
                    var euler = ToEulerAngles(baseRotation);

                    if (lockPitch) euler.X = 0;
                    if (lockRoll)  euler.Y = 0;
                    if (lockYaw)   euler.Z = 0;

                    var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, euler.Z);
                    var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, euler.X);
                    var qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, euler.Y);
                    finalRotation = qz * qx * qy;
                }
                else
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

        private static Vector3 ToEulerAngles(Quaternion q)
        {
            Vector3 euler = new Vector3();

            double w = q.W, x = q.X, y = q.Y, z = q.Z;

            // Matrix elements (derived from quaternion, row-major / transposed)
            double m11 = 1.0 - 2.0 * (y * y + z * z);
            double m12 = 2.0 * (x * y - w * z);
            double m13 = 2.0 * (x * z + w * y);
            double m21 = 2.0 * (x * y + w * z);
            double m22 = 1.0 - 2.0 * (x * x + z * z);
            double m23 = 2.0 * (y * z - w * x);
            double m31 = 2.0 * (x * z - w * y);
            double m32 = 2.0 * (y * z + w * x);
            double m33 = 1.0 - 2.0 * (x * x + y * y);

            // Extract Pitch (X), Roll (Y), Yaw (Z) in ZXY order
            euler.X = (float)Math.Asin(Math.Clamp(m23, -1.0, 1.0));

            if (Math.Abs(m23) < 0.999999f)
            {
                euler.Z = (float)Math.Atan2(-m13, m33);
                euler.Y = (float)Math.Atan2(-m21, m22);
            }
            else
            {
                // Gimbal lock case
                euler.Z = (float)Math.Atan2(m12, m11);
                euler.Y = 0f;
            }

            return euler;
        }
    }
}

