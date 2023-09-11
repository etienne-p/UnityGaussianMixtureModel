using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    partial class ExpectationMaximization
    {
        public void InitializationStep(CommandBuffer cmd, NativeArray<float3> initialCentroids, Texture source, bool useGamma)
        {
            Assert.IsTrue(initialCentroids.Length > 0);
            Assert.IsNotNull(source);

            // Handle whether or not the source requires gamma transformation.
            if (m_InitShader.IsKeywordEnabled(m_UseGammaKeyword) != useGamma)
            {
                if (useGamma)
                {
                    m_InitShader.EnableKeyword(m_UseGammaKeyword);
                }
                else
                {
                    m_InitShader.DisableKeyword(m_UseGammaKeyword);
                }
            }

            m_NumClusters = initialCentroids.Length;
            var numWeights = k_VoxelCount * m_NumClusters;

            m_CentroidBuffer.AllocateIfNeeded(numWeights);
            cmd.SetBufferData(m_CentroidBuffer.In, initialCentroids);
            
            // We encode symmetric matrices using 2 float3.
            m_CovarianceBuffer.AllocateIfNeeded(numWeights * 2);

            ResetColorBins(cmd);
            UpdateColorBins(cmd, source);
            SelectColorBins(cmd);
            UpdateIndirectArgs(cmd);
            ResetCovariances(cmd);
        }

        void ResetColorBins(CommandBuffer cmd)
        {
            Utilities.AllocateBufferIfNeeded<uint>(ref m_ColorBinsBuffer, k_VoxelCount);

            var kernel = m_InitKernelIds.ResetColorBins;
            var shader = m_InitShader;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._ColorBins, m_ColorBinsBuffer);
            cmd.DispatchCompute(shader, kernel, k_VoxelCount / k_GroupSize, 1, 1);
        }

        void UpdateColorBins(CommandBuffer cmd, Texture source)
        {
            var kernel = m_InitKernelIds.UpdateColorBins;
            var shader = m_InitShader;

            cmd.SetComputeVectorParam(shader, ShaderIds._SourceSize, new Vector2(source.width, source.height));
            cmd.SetComputeTextureParam(shader, kernel, ShaderIds._SourceTexture, source);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._ColorBins, m_ColorBinsBuffer);
            var warpX = Mathf.CeilToInt((float)source.width / k_GroupSize);
            var warpY = Mathf.CeilToInt((float)source.height / k_GroupSize);
            cmd.DispatchCompute(shader, kernel, warpX, warpY, 1);
        }

        void SelectColorBins(CommandBuffer cmd)
        {
            Utilities.AllocateBufferIfNeeded<uint2>(ref m_SelectedColorBinBuffer, k_VoxelCount,
                ComputeBufferType.Append);
            cmd.SetBufferCounterValue(m_SelectedColorBinBuffer, 0);

            Utilities.AllocateBufferIfNeeded<uint>(ref m_IndirectArgsBuffer, 4 * 4,
                ComputeBufferType.IndirectArguments);

            var kernel = m_InitKernelIds.SelectColorBins;
            var shader = m_InitShader;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._ColorBins, m_ColorBinsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._AppendSelectedColorBins, m_SelectedColorBinBuffer);
            cmd.DispatchCompute(shader, kernel, k_VoxelCount / k_GroupSize, 1, 1);

            // Copy the count of populated color bins to indirect arguments buffer.
            // We will then infer arguments for subsequent reductions.
            cmd.CopyCounterValue(m_SelectedColorBinBuffer, m_IndirectArgsBuffer, 0);
            // We readback the number of selected color bins, to be used for visualization.
            cmd.RequestAsyncReadbackIntoNativeArray(ref m_NumSelectedColorBins, m_IndirectArgsBuffer, 4, 12,
                OnNumColorBinsReadback);
        }

        void UpdateIndirectArgs(CommandBuffer cmd)
        {
            var kernel = m_InitKernelIds.UpdateIndirectArgs;
            var shader = m_InitShader;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBuffer, m_IndirectArgsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);
        }

        void ResetCovariances(CommandBuffer cmd)
        {
            var kernel = m_InitKernelIds.ResetCovariances;
            var shader = m_InitShader;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesIn, m_CovarianceBuffer.In);

            // We only want to process m_NumClusters items.
            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }
    }
}