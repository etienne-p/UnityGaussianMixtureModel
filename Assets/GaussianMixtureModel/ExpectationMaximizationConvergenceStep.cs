using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    partial class ExpectationMaximization
    {
        const int k_MaxRequiredReductionSteps = 3;

        public void ConvergenceStep(CommandBuffer cmd)
        {
            var numWeights = k_VoxelCount * m_NumClusters;

            cmd.SetComputeIntParam(m_ConvergeShader, ShaderIds._NumClusters, m_NumClusters);

            PreparePrecisionsAndDeterminants(cmd);
            UpdateWeightsAndCentroids(cmd, numWeights);
            ReduceSumsAndCentroids(cmd);
            UpdateCovariances(cmd);
            ReduceCovariances(cmd);
            NormalizeCentroidsAndCovariance(cmd);
        }

        void PreparePrecisionsAndDeterminants(CommandBuffer cmd)
        {
            // We pad buffers to avoid index checks in each kernel execution.
            static int PadToFitGroupSize(int size) => Mathf.CeilToInt(size / (float)k_GroupSize) * k_GroupSize;
            
            Utilities.AllocateBufferIfNeeded<float>(ref m_SqrtDetReciprocalsBuffer,
                PadToFitGroupSize(m_NumClusters));

            // Note the * 2, We encode symmetric matrices using 2 float3.
            Utilities.AllocateBufferIfNeeded<Vector3>(ref m_PrecisionsBuffer,
                PadToFitGroupSize(m_NumClusters * 2));

            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.PreparePrecisionsAndDeterminants;

            cmd.SetComputeIntParam(shader, ShaderIds._NumClusters, m_NumClusters);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._Covariances, m_CovarianceBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._PrecisionsRW, m_PrecisionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SqrtDetReciprocalsRW, m_SqrtDetReciprocalsBuffer);

            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }

        void UpdateWeightsAndCentroids(CommandBuffer cmd, int numWeights)
        {
            Utilities.AllocateBufferIfNeeded<Vector3>(ref m_WeightsBuffer, numWeights);

            // TODO Optimization can allocate less memory for the Out buffer.
            // TODO Is there a way to apply normalization earlier?
            m_SumBuffer.AllocateIfNeeded(numWeights);

            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.UpdateWeightsAndCentroids;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBuffer, m_IndirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._Weights, m_WeightsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CentroidsIn, m_CentroidBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CentroidsOut, m_CentroidBuffer.Out);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SqrtDetReciprocals, m_SqrtDetReciprocalsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._Precisions, m_PrecisionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SelectedColorBins, m_SelectedColorBinBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SumsOut, m_SumBuffer.Out);
            cmd.DispatchCompute(shader, kernel, m_IndirectArgsBuffer, 0);
        }
        
        void ReduceSumsAndCentroids(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.ReduceSumsAndCentroids;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBuffer, m_IndirectArgsBuffer);

            for (var i = 0; i != k_MaxRequiredReductionSteps; ++i)
            {
                m_SumBuffer.Swap();
                m_CentroidBuffer.Swap();
                cmd.SetComputeIntParam(shader, ShaderIds._IndirectArgsOffset, 4 * i);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SumsIn, m_SumBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SumsOut, m_SumBuffer.Out);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CentroidsIn, m_CentroidBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CentroidsOut, m_CentroidBuffer.Out);

                for (var k = 0; k != m_NumClusters; ++k)
                {
                    cmd.SetComputeIntParam(shader, ShaderIds._ClusterIndex, k);
                    cmd.DispatchCompute(shader, kernel, m_IndirectArgsBuffer, 4u * (uint)i * sizeof(uint));
                }
            }

            m_SumBuffer.Swap();
            m_CentroidBuffer.Swap();
        }

        void UpdateCovariances(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.UpdateCovariances;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBuffer, m_IndirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SelectedColorBins, m_SelectedColorBinBuffer);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CentroidsIn, m_CentroidBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SumsIn, m_SumBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._Weights, m_WeightsBuffer);
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

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._IndirectArgsBuffer, m_IndirectArgsBuffer);

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
        
        void NormalizeCentroidsAndCovariance(CommandBuffer cmd)
        {
            var shader = m_ConvergeShader;
            var kernel = m_ConvergeKernelIds.NormalizeCentroidsAndCovariances;

            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CovariancesIn, m_CovarianceBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._CentroidsIn, m_CentroidBuffer.In);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIds._SumsIn, m_SumBuffer.In);

            // We only want to process m_NumClusters items.
            var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
            cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
        }
    }
}