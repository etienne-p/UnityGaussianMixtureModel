using System;
using Unity.Mathematics;
using UnityEngine;

namespace GaussianMixtureModel
{
    [Serializable]
    public struct OrbitTransform
    {
        public const float k_MaxPosition = 4;

        [Range(0, k_MaxPosition)] public float Position;
        [Range(-89, 89)] public float Pitch;
        [Range(0, 360)] public float Yaw;

        public OrbitTransform Validate()
        {
            Position = math.clamp(Position, 0, k_MaxPosition);
            Pitch = math.clamp(Pitch, -89, 89);
            return this;
        }

        public int GetPropertiesHashCode()
        {
            unchecked
            {
                var hashCode = Position.GetHashCode();
                hashCode = (hashCode * 397) ^ Pitch.GetHashCode();
                return (hashCode * 397) ^ Yaw.GetHashCode();
            }
        }

        public float4x4 GetViewProjection(float aspect) => GetViewProjection(this, aspect);

        static float4x4 GetViewProjection(OrbitTransform cameraTransform, float aspect)
        {
            var model = float4x4.Translate(Vector3.one * -.5f);

            var rotation = math.mul(
                quaternion.AxisAngle(Vector3.up, math.radians(cameraTransform.Yaw)),
                quaternion.AxisAngle(Vector3.right, math.radians(cameraTransform.Pitch)));

            var cameraPosition = math.rotate(rotation, Vector3.forward) * cameraTransform.Position;
            var view = math.inverse(float4x4.LookAt(cameraPosition, Vector3.zero, Vector3.up));

            GetFrustumPlanes(cameraPosition, view, out var near, out var far);
            near = math.max(.1f, near);
            far = math.max(near + .1f, far);

            var projection = float4x4.PerspectiveFov(math.radians(60), aspect, near, far);

            // FLip Z.
            projection.c2 *= -1;

            var modelView = math.mul(view, model);

            projection = GL.GetGPUProjectionMatrix(projection, true);
            return math.mul(projection, modelView);
        }

        // We calculate frustum planes to best fit a centered unit cube whose center the camera looks at.
        static void GetFrustumPlanes(float3 cameraPosition, float4x4 view, out float near, out float far)
        {
            // Find the closest and furthest point on the cube,
            var closest = math.step(float3.zero, cameraPosition) * 2 - 1;
            var furthest = -closest;

            // Project these points on the view axis.
            var closestViewSpace = math.mul(view, new float4(closest, 1));
            var furthestViewSpace = math.mul(view, new float4(furthest, 1));
            near = closestViewSpace.z / closestViewSpace.w;
            far = furthestViewSpace.z / furthestViewSpace.w;
        }
    }
}
