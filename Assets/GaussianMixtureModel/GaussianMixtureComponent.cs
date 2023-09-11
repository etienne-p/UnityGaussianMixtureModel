using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianMixtureModel
{
    struct VisualizationBuffers
    {
        public ComputeBuffer SelectedColorBins;
        public ComputeBuffer Means;
        public ComputeBuffer Covariances;
    }

    [ExecuteAlways]
    public class GaussianMixtureComponent : MonoBehaviour
    {
        const int k_MinIterations = 1;
        const int k_MaxIterations = 64;
        const int k_MinClusters = 2;
        const int k_MaxClusters = 32;

        [Tooltip("Assign \"Initialization.compute\".")] 
        [SerializeField]
        ComputeShader m_InitShader;

        [Tooltip("Assign \"Converge.compute\".")] 
        [SerializeField]
        ComputeShader m_ConvergeShader;

        [Tooltip("Assign \"Cholesky.compute\".")] 
        [SerializeField]
        ComputeShader m_CholeskyShader;

        [Tooltip("The image to be analyzed.")] 
        [SerializeField]
        Texture m_Source;

        [Tooltip("The number of clusters.")] 
        [SerializeField] 
        [Range(k_MinClusters, k_MaxClusters)]
        int m_NumClusters;

        [Tooltip("The number of convergence steps of Expectation Maximization.")]
        [SerializeField]
        [Range(k_MinIterations, k_MaxIterations)]
        int m_Iterations;

        [Tooltip("The delay elapsed between each convergence step.")]
        [SerializeField] 
        [Range(.01f, .5f)] 
        float m_Delay;

        readonly Visualizer m_Visualizer = new();
        readonly ExpectationMaximization m_ExpectationMaximization = new();
        CommandBuffer m_ComputeCommandBuffer;
        int m_NumSelectedColorBins;
        int m_BuffersGeneration;
        bool m_PendingComputeCapture;

        void SetNumSelectedColorBins(int count) => m_NumSelectedColorBins = count;

        void OnEnable()
        {
            m_ComputeCommandBuffer = new CommandBuffer
            {
                name = "Expectation-Maximization"
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

            m_PendingComputeCapture = false;
            SetContinuousEditorUpdate(false);
            m_NumSelectedColorBins = 0;
        }

        void OnValidate()
        {
            m_NumClusters = math.clamp(m_NumClusters, k_MinClusters, k_MaxClusters);
            m_Iterations = math.clamp(m_Iterations, k_MinIterations, k_MaxIterations);
        }

        public int GetVisualizationHashcode()
        {
            var visualizationHashcode = m_NumClusters;

            unchecked
            {
                visualizationHashcode = (visualizationHashcode * 397) ^ m_BuffersGeneration;
            }

            return visualizationHashcode;
        }

        public bool TryRenderVisualization(CommandBuffer cmd, RenderTexture target, float4x4 viewProjection)
        {
            if (m_Source == null || m_NumSelectedColorBins == 0)
            {
                return false;
            }

            var buffers = new VisualizationBuffers
            {
                SelectedColorBins = m_ExpectationMaximization.SelectedColorBinsBuffer,
                Means = m_ExpectationMaximization.MeansBuffer,
                Covariances = m_ExpectationMaximization.CovariancesBuffer
            };

            if (buffers.SelectedColorBins == null ||
                buffers.Means == null ||
                buffers.Covariances == null)
            {
                return false;
            }

            // In marginal case we could imagine the source changing so that it is out-of-sync with compute buffers.
            // We accept this for the time being.
            var totalSamples = m_Source.width * m_Source.height;

            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.black);

            m_Visualizer.Render(
                cmd, viewProjection,
                totalSamples, m_NumClusters, m_NumSelectedColorBins, buffers);
            return true;
        }

        [ContextMenu("DEBUG - Compute Capture")]
        void ScheduleCapture() => m_PendingComputeCapture = true;

        [ContextMenu("Execute")]
        public void StartExecution()
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

            if (!m_Source.isReadable)
            {
                Debug.LogError($"Texture {m_Source.name} is not readable. Select the \"Read/Write\" checkbox in its inspector.");
                yield break;
            }

            SetContinuousEditorUpdate(true);
            m_NumSelectedColorBins = 0;

            var initialMeans = new NativeArray<float3>(m_NumClusters, Allocator.Temp);

            // To initialize Means we use evenly distributed hues.
            for (var i = 0; i != initialMeans.Length; ++i)
            {
                initialMeans[i] = Color.HSVToRGB((float)i / initialMeans.Length, .5f, .5f).ToFloat3();
            }

            var totalSamples = m_Source.width * m_Source.height;

            m_ExpectationMaximization.InitializationStep(m_ComputeCommandBuffer, initialMeans, m_Source);

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

                m_ExpectationMaximization.ConvergenceStep(m_ComputeCommandBuffer, totalSamples);

                ExecuteCommandBuffer(m_ComputeCommandBuffer, capture);
                ++m_BuffersGeneration;
            }

            SetContinuousEditorUpdate(false);
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
        
        static bool s_ContinuousEditorUpdate;

        static void SetContinuousEditorUpdate(bool value)
        {
#if UNITY_EDITOR
            if (s_ContinuousEditorUpdate == value)
            {
                return;
            }

            s_ContinuousEditorUpdate = value;

            if (s_ContinuousEditorUpdate)
            {
                UnityEditor.EditorApplication.update += QueueUpdate;
            }
            else
            {
                UnityEditor.EditorApplication.update -= QueueUpdate;
            }
#endif
        }
        
        static void QueueUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }
    }
}