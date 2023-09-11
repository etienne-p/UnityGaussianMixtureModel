using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    partial class ExpectationMaximization
    {
        public void InitializationStep(CommandBuffer cmd, NativeArray<float3> initialMeans, Texture source)
        {
            Assert.IsTrue(initialMeans.Length > 0);
            Assert.IsNotNull(source);

            if (initialMeans.Length > k_MaxClusters)
            {
                throw new InvalidOperationException($"Supports up to {k_MaxClusters}, passed {initialMeans.Length}.");
            }

            // Reset previous value, we monitor it for visualization.
            m_NumSelectedColorBins[0] = 0;

            m_NumClusters = initialMeans.Length;
            var numWeights = k_VoxelCount * m_NumClusters;

            m_CentroidBuffer.AllocateIfNeeded(numWeights);
            m_CovarianceBuffer.AllocateIfNeeded(numWeights);
            Utilities.AllocateBufferIfNeeded<float>(ref m_FracsBuffer, m_NumClusters);

            cmd.SetBufferData(m_CentroidBuffer.In, initialMeans);
            cmd.SetComputeIntParam(m_InitShader, ShaderIds._NumClusters, m_NumClusters);

            ResetColorBins(cmd);
            UpdateColorBins(cmd, source);
            SelectColorBins(cmd);
            UpdateIndirectArgs(cmd);
            ResetCovariancesAndFracs(cmd);
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
        }

        void UpdateIndirectArgs(CommandBuffer cmd)
        {
            var kernel = m_InitKernelIds.UpdateIndirectArgs;
            var shader = m_InitShader;

            // Copy the count of populated color bins to indirect arguments buffer.
            // We will then infer arguments for subsequent reductions.
            cmd.CopyCounterValue(m_SelectedColorBinBuffer, m_IndirectArgsBuffer, 0);

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBufferOut, m_IndirectArgsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            // We readback the number of selected color bins, to be used for visualization.
            cmd.RequestAsyncReadbackIntoNativeArray(ref m_NumSelectedColorBins, m_IndirectArgsBuffer, 4, 12,
                OnNumColorBinsReadback);
        }

        void ResetCovariancesAndFracs(CommandBuffer cmd)
        {
            var kernel = m_InitKernelIds.ResetCovariancesAndFracs;
            var shader = m_InitShader;

            // Covariance is modified *in place*.
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesOut, m_CovarianceBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._FracsOut, m_FracsBuffer);

            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }
    }
}
