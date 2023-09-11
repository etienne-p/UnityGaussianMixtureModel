using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    /// <summary>
    /// </summary>
    partial class ExpectationMaximization
    {
        class InitKernelIds
        {
            public int ResetColorBins;
            public int UpdateColorBins;
            public int SelectColorBins;
            public int UpdateIndirectArgs;
            public int ResetCovariances;
        }
        
        class ConvergeKernelIds
        {
            public int PreparePrecisionsAndDeterminants;
            public int UpdateWeightsAndCentroids;
            public int ReduceSumsAndCentroids;
            public int UpdateCovariances;
            public int ReduceCovariances;
            public int NormalizeCentroidsAndCovariances;
        }

        const int k_GroupSize = 32;

        const int k_GridSize = 32;
        const int k_VoxelCount = k_GridSize * k_GridSize * k_GridSize;

        readonly DoubleBuffer<float> m_SumBuffer = new();
        readonly DoubleBuffer<float3> m_CentroidBuffer = new();
        readonly DoubleBuffer<float3> m_CovarianceBuffer = new();
        ComputeBuffer m_ColorBinsBuffer;
        ComputeBuffer m_PrecisionsBuffer;
        ComputeBuffer m_SqrtDetReciprocalsBuffer;
        ComputeBuffer m_WeightsBuffer;
        ComputeBuffer m_SelectedColorBinBuffer;
        ComputeBuffer m_IndirectArgsBuffer;
        NativeArray<uint> m_NumSelectedColorBins;
        ComputeShader m_InitShader;
        ComputeShader m_ConvergeShader;
        LocalKeyword m_UseGammaKeyword;
        readonly InitKernelIds m_InitKernelIds = new();
        readonly ConvergeKernelIds m_ConvergeKernelIds = new();
        int m_NumClusters;

        // Exposed as it is useful for visualization.
        public Action<int> NumColorBinsEvaluated = delegate { };
        public ComputeBuffer CentroidsBuffer => m_CentroidBuffer.In;
        public ComputeBuffer CovariancesBuffer => m_CovarianceBuffer.In;
        public ComputeBuffer SelectedColorBinsBuffer => m_SelectedColorBinBuffer;

        void OnNumColorBinsReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                throw new InvalidOperationException("Failed to readback number of color bins.");
            }

            NumColorBinsEvaluated.Invoke((int)m_NumSelectedColorBins[0]);
        }

        public void Initialize(ComputeShader initShader, ComputeShader convergeShader)
        {
            m_InitShader = initShader;
            m_ConvergeShader = convergeShader;
            m_UseGammaKeyword = new LocalKeyword(m_InitShader, "USE_GAMMA_SPACE");
            Utilities.LoadKernelIndices(m_InitShader, m_InitKernelIds);
            Utilities.LoadKernelIndices(m_ConvergeShader, m_ConvergeKernelIds);
            Utilities.AllocateNativeArrayIfNeeded(ref m_NumSelectedColorBins, 1);
        }

        public void Dispose()
        {
            Utilities.DeallocateNativeArrayIfNeeded(ref m_NumSelectedColorBins);
            m_SumBuffer.Dispose();
            m_CentroidBuffer.Dispose();
            m_CovarianceBuffer.Dispose();
            Utilities.DeallocateIfNeeded(ref m_PrecisionsBuffer);
            Utilities.DeallocateIfNeeded(ref m_SqrtDetReciprocalsBuffer);
            Utilities.DeallocateIfNeeded(ref m_WeightsBuffer);
            Utilities.DeallocateIfNeeded(ref m_SelectedColorBinBuffer);
            Utilities.DeallocateIfNeeded(ref m_IndirectArgsBuffer);
            Utilities.DeallocateIfNeeded(ref m_ColorBinsBuffer);
        }
    }
}