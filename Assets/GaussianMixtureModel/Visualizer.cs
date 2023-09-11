using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
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

        public void Render(CommandBuffer cmd, float4x4 viewProjection,
            int totalSamples, int numClusters, int numColorBins, VisualizationBuffers buffers)
        {
            // Draw scaled cubes representing the color bins (voxel grid).
            {
                // A heuristic to scale voxels.
                var maxBinSize = 4 * (float)totalSamples / numColorBins;

                var block = m_VoxelsPropertyBlock;
                block.SetMatrix(ShaderIds._ViewProjection, viewProjection);
                block.SetFloat(ShaderIds._MaxBinSize, maxBinSize);
                block.SetFloat(ShaderIds._Opacity, .1f);
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
                block.SetFloat(ShaderIds._Opacity, .9f);
                block.SetBuffer(ShaderIds._Means, buffers.Means);
                block.SetBuffer(ShaderIds._Cholesky, m_CholeskyBuffer);

                cmd.DrawMeshInstancedProcedural(m_SphereMesh, 0, m_EllipsoidsMaterial, 0, numClusters, block);
            }
        }
    }
}