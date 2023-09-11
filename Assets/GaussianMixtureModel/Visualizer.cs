using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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
    }

    class Visualizer
    {
        const int k_GroupSize = 16;

        ComputeBuffer m_CholeskyBuffer;
        Material m_EllipsoidsMaterial;
        Material m_VoxelsMaterial;
        MaterialPropertyBlock m_EllipsoidsPropertyBlock;
        MaterialPropertyBlock m_VoxelsPropertyBlock;
        ComputeShader m_DecomposeCovarianceShader;
        Mesh m_SphereMesh;
        Mesh m_CubeMesh;

        public void Initialize(ComputeShader decomposeCovarianceShader)
        {
            m_DecomposeCovarianceShader = decomposeCovarianceShader;
            
            m_VoxelsPropertyBlock ??= new MaterialPropertyBlock();
            m_EllipsoidsPropertyBlock ??= new MaterialPropertyBlock();
            
            // Fetch built-in meshes.
            m_SphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            m_CubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            m_EllipsoidsMaterial = new Material(Shader.Find("Custom/GaussianMixtureModel/Ellipsoid"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            
            m_VoxelsMaterial = new Material(Shader.Find("Custom/GaussianMixtureModel/Voxels"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public void Dispose()
        {
            Utilities.Destroy(m_EllipsoidsMaterial);
            Utilities.Destroy(m_VoxelsMaterial);
            Utilities.DeallocateIfNeeded(ref m_CholeskyBuffer);
        }
        
        public void Render(CommandBuffer cmd, RenderTexture target, OrbitTransform orbitViewTransform, 
            int totalSamples, int numClusters, int numColorBins, VisualizationBuffers buffers)
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.black);

            var viewProjection = GetViewProjection(orbitViewTransform, (float)target.width / target.height);

            // Draw scaled cubes representing the color bins (voxel grid).
            {
                // A heuristic to scale voxels.
                var maxBinSize = 4 * (float)totalSamples / numColorBins;

                var block = m_VoxelsPropertyBlock;
                block.SetMatrix(ShaderIds._ViewProjection, viewProjection);
                block.SetFloat(ShaderIds._MaxBinSize, maxBinSize);
                block.SetFloat(ShaderIds._Opacity, .5f);
                block.SetBuffer(ShaderIds._ColorBins, buffers.SelectedColorBins);

                cmd.DrawMeshInstancedProcedural(m_CubeMesh, 0, m_VoxelsMaterial, 0, numColorBins, block);
            }
            
            // Decompose covariances. These matrices will allow us to morph spheres into ellipsoids.
            {
                Utilities.AllocateBufferIfNeeded<float3x3>(ref m_CholeskyBuffer, numClusters);

                var shader = m_DecomposeCovarianceShader;
                var kernel = 0; // There is only 1 kernel in that shader.

                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._Covariances, buffers.Covariances);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._Cholesky, m_CholeskyBuffer);

                var warpX = Mathf.CeilToInt((float)numClusters / k_GroupSize);
                cmd.DispatchCompute(shader, kernel, warpX, 1, 1);
            }
            
            // Draw ellipsoids representing the clusters' covariances.
            {
                var block = m_EllipsoidsPropertyBlock;
                block.SetMatrix(ShaderIds._ViewProjection, viewProjection);
                block.SetFloat(ShaderIds._Opacity, .5f);
                block.SetBuffer(ShaderIds._Centroids, buffers.Centroids);
                block.SetBuffer(ShaderIds._Cholesky, m_CholeskyBuffer);

                cmd.DrawMeshInstancedProcedural(m_SphereMesh, 0, m_EllipsoidsMaterial, 0, numClusters, block);
            }
        }

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