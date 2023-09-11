using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    struct VisualizationBuffers
    {
        public ComputeBuffer SelectedColorBins;
        public ComputeBuffer Centroids;
        public ComputeBuffer Covariances;
    }

    [ExecuteAlways]
    public class GaussianMixtureModelDemo : MonoBehaviour
    {
        const int k_MinIterations = 1;
        const int k_MaxIterations = 64;
        const int k_MinClusters = 2;
        const int k_MaxClusters = 32;
        const int k_GuiSize = 512;

        [SerializeField] ComputeShader m_InitShader;
        [SerializeField] ComputeShader m_ConvergeShader;
        [SerializeField] ComputeShader m_CholeskyShader;

        [Tooltip("The image to be analyzed.")] 
        [SerializeField]
        Texture m_Source;

        [Tooltip("The number of clusters.")] 
        [SerializeField] 
        [Range(k_MinClusters, k_MaxClusters)]
        int m_NumClusters;

        [Tooltip("The number of iterations of Expectation Maximization.")]
        [SerializeField]
        [Range(k_MinIterations, k_MaxIterations)]
        int m_Iterations;

        [SerializeField] [Range(.01f, .5f)] float m_Delay;

        [SerializeField] OrbitTransform m_View;

        readonly Visualizer m_Visualizer = new();
        readonly ExpectationMaximization m_ExpectationMaximization = new();
        CommandBuffer m_ComputeCommandBuffer;
        CommandBuffer m_VisualizeCommandBuffer;
        RenderTexture m_VisualizationTarget;
        int m_VisualizationHashcode;
        int m_NumSelectedColorBins;
        int m_BuffersGeneration;
        bool m_PendingComputeCapture;
        bool m_PendingVisualizationCapture;
        bool m_PendingViewUpdate;
        bool m_ShouldSchedulePlayerLoopUpdate;
        readonly Rect m_GuiRect = new(0, 0, k_GuiSize, k_GuiSize);

        void SetNumSelectedColorBins(int count) => m_NumSelectedColorBins = count;

        void OnEnable()
        {
            m_ComputeCommandBuffer = new CommandBuffer
            {
                name = "Expectation-Maximization"
            };

            m_VisualizeCommandBuffer = new CommandBuffer
            {
                name = "Visualization"
            };

            m_ExpectationMaximization.Initialize(m_InitShader, m_ConvergeShader);
            m_Visualizer.Initialize(m_CholeskyShader);
            m_ExpectationMaximization.NumColorBinsEvaluated += SetNumSelectedColorBins;
        }

        void OnDisable()
        {
            m_ExpectationMaximization.NumColorBinsEvaluated -= SetNumSelectedColorBins;
            StopAllCoroutines();
            
            m_ExpectationMaximization.Dispose();
            m_Visualizer.Dispose();
            m_ComputeCommandBuffer.Dispose();
            m_VisualizeCommandBuffer.Dispose();
            
            Utilities.DeallocateIfNeeded(ref m_VisualizationTarget);
            
            m_PendingComputeCapture = false;
            m_PendingVisualizationCapture = false;
            m_PendingViewUpdate = false;
            m_ShouldSchedulePlayerLoopUpdate = false;
            m_NumSelectedColorBins = 0;
        }

        void OnValidate()
        {
            m_NumClusters = math.clamp(m_NumClusters, k_MinClusters, k_MaxClusters);
            m_Iterations = math.clamp(m_Iterations, k_MinIterations, k_MaxIterations);
        }

        void OnGUI()
        {
            if (m_VisualizationTarget != null)
            {
                GUI.DrawTexture(m_GuiRect, m_VisualizationTarget);
            }
        }

        void Update()
        {
            if (m_Source == null || m_NumSelectedColorBins == 0)
            {
                return;
            }

            var visualizationHashcode = m_View.GetPropertiesHashCode();

            unchecked
            {
                visualizationHashcode = (visualizationHashcode * 397) ^ m_NumClusters;
                visualizationHashcode = (visualizationHashcode * 397) ^ m_BuffersGeneration;
            }

            if (visualizationHashcode != m_VisualizationHashcode)
            {
                m_VisualizationHashcode = visualizationHashcode;

                var buffers = new VisualizationBuffers
                {
                    SelectedColorBins = m_ExpectationMaximization.SelectedColorBinsBuffer,
                    Centroids = m_ExpectationMaximization.CentroidsBuffer,
                    Covariances = m_ExpectationMaximization.CovariancesBuffer
                };

                UpdateVisualization(buffers);
            }

#if UNITY_EDITOR
            if (m_ShouldSchedulePlayerLoopUpdate)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        void UpdateVisualization(VisualizationBuffers resultBuffers)
        {
            Utilities.AllocateIfNeededForCompute(ref m_VisualizationTarget, k_GuiSize, k_GuiSize,
                GraphicsFormat.R8G8B8A8_UNorm);

            // In marginal case we could imagine the source changing so that it is out-of-sync with compute buffers.
            // We accept this for the time being.
            var totalSamples = m_Source.width * m_Source.height;

            m_Visualizer.Render(
                m_VisualizeCommandBuffer, m_VisualizationTarget, m_View,
                totalSamples, m_NumClusters, m_NumSelectedColorBins, resultBuffers);

            ExecuteCommandBuffer(m_VisualizeCommandBuffer, m_PendingVisualizationCapture);
            m_PendingVisualizationCapture = false;
        }

        [ContextMenu("DEBUG - Compute Capture")]
        void ScheduleCapture() => m_PendingComputeCapture = true;

        [ContextMenu("DEBUG - Visualization Capture")]
        void ScheduleVizCapture() => m_PendingVisualizationCapture = true;

        [ContextMenu("Execute")]
        void StartExecution()
        {
            StopAllCoroutines();
            StartCoroutine(Execute(m_PendingComputeCapture, m_Iterations));
            m_PendingComputeCapture = false;
        }

        IEnumerator Execute(bool capture, int iterations)
        {
            if (m_Source == null)
            {
                Debug.LogError($"Assign a non-null value to {nameof(m_Source)}.");
                yield break;
            }

            m_ShouldSchedulePlayerLoopUpdate = true;
            m_NumSelectedColorBins = 0;

            var initialCentroids = new NativeArray<float3>(m_NumClusters, Allocator.Temp);
            Utilities.AllocateNativeArrayIfNeeded(ref initialCentroids, m_NumClusters);

            // To initialize centroids we use evenly distributed hues.
            for (var i = 0; i != initialCentroids.Length; ++i)
            {
                initialCentroids[i] = Color.HSVToRGB((float)i / initialCentroids.Length, .5f, .5f).ToFloat3();
            }

            // TODO Infer useGamma from source?
            m_ExpectationMaximization.InitializationStep(m_ComputeCommandBuffer, initialCentroids, m_Source, true);

            ExecuteCommandBuffer(m_ComputeCommandBuffer, capture);
            ++m_BuffersGeneration;
            
            // Wait for readback completion.
            while (m_NumSelectedColorBins == 0)
            {
                yield return null;
            }

            for (var i = 0; i != iterations; ++i)
            {
                yield return new WaitForSeconds(m_Delay);

                m_ExpectationMaximization.ConvergenceStep(m_ComputeCommandBuffer);

                ExecuteCommandBuffer(m_ComputeCommandBuffer, capture);
                ++m_BuffersGeneration;
            }

            m_ShouldSchedulePlayerLoopUpdate = false;
        }

        static void ExecuteCommandBuffer(CommandBuffer cmd, bool capture)
        {
            if (capture)
            {
                using (RenderDocCaptureScopeFactory.Create())
                {
                    Graphics.ExecuteCommandBuffer(cmd);
                }
            }
            else
            {
                Graphics.ExecuteCommandBuffer(cmd);
            }

            cmd.Clear();
        }
    }
}