using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    partial class ExpectationMaximization
    {
        const int k_MaxRequiredReductionSteps = 3;

        public void ConvergenceStep(CommandBuffer cmd, float totalSamples)
        {
            var numWeights = k_VoxelCount * m_NumClusters;

            cmd.SetComputeIntParam(m_ConvergeShader, ShaderIds._NumClusters, m_NumClusters);

            PrepareCholeskysAndLnDets(cmd);
            UpdateRespsAndMeans(cmd, numWeights);
            ReduceWeightsAndMeans(cmd);
            NormalizeMeansAndFracs(cmd, totalSamples);
            UpdateCovariances(cmd);
            ReduceCovariances(cmd);
            NormalizeCovariances(cmd);
        }

        void PrepareCholeskysAndLnDets(CommandBuffer cmd)
        {
            Utilities.AllocateBufferIfNeeded<float>(ref m_LnDetsBuffer, m_NumClusters);
            Utilities.AllocateBufferIfNeeded<float3x3>(ref m_CholeskysBuffer, m_NumClusters);

            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.PrepareCholeskysAndLnDets;

            cmd.SetComputeIntParam(shader, ShaderIds._NumClusters, m_NumClusters);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesIn, m_CovarianceBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CholeskysOut, m_CholeskysBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._LnDetsOut, m_LnDetsBuffer);

            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }

        void UpdateRespsAndMeans(CommandBuffer cmd, int numWeights)
        {
            Utilities.AllocateBufferIfNeeded<float>(ref m_RespsBuffer, numWeights);
            m_WeightsBuffer.AllocateIfNeeded(numWeights);
            
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.UpdateRespsAndMeans;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SelectedColorBins, m_SelectedColorBinBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBufferIn, m_IndirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._MeansIn, m_CentroidBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CholeskysIn, m_CholeskysBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._LnDetsIn, m_LnDetsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._FracsIn, m_FracsBuffer);

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._RespsOut, m_RespsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._WeightsOut, m_WeightsBuffer.Out);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._MeansOut, m_CentroidBuffer.Out);

            cmd.DispatchCompute(shader, kernel, m_IndirectArgsBuffer, 0);
        }
        
        void ReduceWeightsAndMeans(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.ReduceWeightsAndMeans;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBufferIn, m_IndirectArgsBuffer);

            for (var i = 0; i != k_MaxRequiredReductionSteps; ++i)
            {
                m_WeightsBuffer.Swap();
                m_CentroidBuffer.Swap();
                cmd.SetComputeIntParam(shader, ShaderIds._IndirectArgsOffset, 4 * i);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._WeightsIn, m_WeightsBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._WeightsOut, m_WeightsBuffer.Out);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._MeansIn, m_CentroidBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._MeansOut, m_CentroidBuffer.Out);

                for (var k = 0; k != m_NumClusters; ++k)
                {
                    cmd.SetComputeIntParam(shader, ShaderIds._ClusterIndex, k);
                    cmd.DispatchCompute(shader, kernel, m_IndirectArgsBuffer, 4u * (uint)i * sizeof(uint));
                }
            }

            m_WeightsBuffer.Swap();
            m_CentroidBuffer.Swap();
        }

        void NormalizeMeansAndFracs(CommandBuffer cmd, float totalSamples)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.NormalizeMeansAndFracs;

            // Means are normalized *in place*.
            cmd.SetComputeFloatParam(shader, ShaderIds._TotalSamples, totalSamples);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._FracsOut, m_FracsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._WeightsIn, m_WeightsBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._MeansOut, m_CentroidBuffer.In);

            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }
        
        void UpdateCovariances(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.UpdateCovariances;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBufferIn, m_IndirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SelectedColorBins, m_SelectedColorBinBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._MeansIn, m_CentroidBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._RespsIn, m_RespsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesOut, m_CovarianceBuffer.Out);

            for (var k = 0; k != m_NumClusters; ++k)
            {
                cmd.SetComputeIntParam(shader, ShaderIds._ClusterIndex, k);
                cmd.DispatchCompute(shader, kernel, m_IndirectArgsBuffer, 0);
            }
        }

        void ReduceCovariances(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.ReduceCovariances;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBufferIn, m_IndirectArgsBuffer);

            for (var i = 0; i != k_MaxRequiredReductionSteps; ++i)
            {
                m_CovarianceBuffer.Swap();
                cmd.SetComputeIntParam(shader, ShaderIds._IndirectArgsOffset, 4 * i);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesIn, m_CovarianceBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesOut, m_CovarianceBuffer.Out);

                for (var k = 0; k != m_NumClusters; ++k)
                {
                    cmd.SetComputeIntParam(shader, ShaderIds._ClusterIndex, k);
                    cmd.DispatchCompute(shader, kernel, m_IndirectArgsBuffer, 4u * (uint)i * sizeof(uint));
                }
            }

            m_CovarianceBuffer.Swap();
        }
        
        void NormalizeCovariances(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.NormalizeCovariances;

            // Covariance is normalized *in-place*.
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._WeightsIn, m_WeightsBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesOut, m_CovarianceBuffer.In);

            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }
    }
}