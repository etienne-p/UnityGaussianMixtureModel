using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace GaussianMixtureModel.Editor
{
    interface IHasOrbitTransform
    {
        OrbitTransform OrbitTransform { get; set; }
    }

    class ViewManipulator : MouseManipulator
    {
        IHasOrbitTransform m_HasOrbitTransform;
        bool m_Active;

        public ViewManipulator(IHasOrbitTransform hasOrbitTransform)
        {
            m_HasOrbitTransform = hasOrbitTransform;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.MiddleMouse });
            m_Active = false;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<WheelEvent>(OnWheelEvent);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<WheelEvent>(OnWheelEvent);
        }

        void OnMouseDown(MouseDownEvent e)
        {
            if (m_Active)
            {
                e.StopImmediatePropagation();
                return;
            }

            if (CanStartManipulation(e))
            {
                m_Active = true;
                target.CaptureMouse();
                e.StopPropagation();
            }
        }

        void OnMouseMove(MouseMoveEvent e)
        {
            if (!m_Active || !target.HasMouseCapture())
            {
                return;
            }

            var trs = m_HasOrbitTransform.OrbitTransform;
            trs.Pitch -= e.mouseDelta.y * .5f;
            trs.Yaw += e.mouseDelta.x * .5f;
            m_HasOrbitTransform.OrbitTransform = trs.Validate();
            e.StopPropagation();
        }

        void OnMouseUp(MouseUpEvent e)
        {
            if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(e))
            {
                return;
            }

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }

        void OnWheelEvent(WheelEvent e)
        {
            var trs = m_HasOrbitTransform.OrbitTransform;
            trs.Position += e.delta.y / 50.0f;
            m_HasOrbitTransform.OrbitTransform = trs.Validate();
            e.StopPropagation();
        }
    }

    class VisualizationWindow : EditorWindow, IHasOrbitTransform
    {
        [MenuItem("Window/Gaussian Mixture/Visualization")]
        public static void ShowWindow()
        {
            GetWindow<VisualizationWindow>().Show();
        }

        [SerializeField]
        OrbitTransform m_OrbitTransform;

        VisualElement m_HelpBox;
        VisualElement m_Container;
        Image m_Image;
        RenderTexture m_Target;
        CommandBuffer m_CommandBuffer;
        GaussianMixtureModelComponent m_GaussianMixtureModel;
        int m_RenderHashcode;

        public OrbitTransform OrbitTransform
        {
            get => m_OrbitTransform;
            set => m_OrbitTransform = value;
        }

        public void CreateGUI()
        {
            m_HelpBox = new HelpBox($"Select a game object holding a {nameof(GaussianMixtureModelComponent)} component in the hierarchy.", HelpBoxMessageType.Error);
            rootVisualElement.Add(m_HelpBox);

            m_Container = new VisualElement();
            m_Container.style.flexGrow = 1;
            rootVisualElement.Add(m_Container);

            var executeButton = new Button(TryStartExecution) { text = "Execute Expectation Maximization" };
            m_Container.Add(executeButton);

            m_Image = new Image();
            m_Image.style.flexGrow = 1;
            m_Image.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_Image.AddManipulator(new ViewManipulator(this));
            m_Container.Add(m_Image);

            SelectionChanged();
        }

        void OnEnable()
        {
            m_CommandBuffer = new CommandBuffer
            {
                name = "Visualization"
            };
            SelectionChanged();
            Selection.selectionChanged += SelectionChanged;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= SelectionChanged;
            Utilities.DeallocateIfNeeded(ref m_Target);
            m_CommandBuffer.Dispose();
        }

        void Update()
        {
            if (m_GaussianMixtureModel == null || m_Target == null)
            {
                return;
            }

            var renderHashcode = m_OrbitTransform.GetPropertiesHashCode();
            unchecked
            {
                renderHashcode = (renderHashcode * 397) ^ m_GaussianMixtureModel.GetVisualizationHashcode();
                renderHashcode = (renderHashcode * 397) ^ m_Target.width;
                renderHashcode = (renderHashcode * 397) ^ m_Target.height;
            }

            var aspect = m_Target.width / (float)m_Target.height;

            if (renderHashcode != m_RenderHashcode && m_GaussianMixtureModel.TryRenderVisualization(
                m_CommandBuffer, m_Target, m_OrbitTransform.GetViewProjection(aspect)))
            {
                Graphics.ExecuteCommandBuffer(m_CommandBuffer);
                m_CommandBuffer.Clear();
                Repaint();
                m_RenderHashcode = renderHashcode;
            }
        }

        void ShowHelpBox(bool value)
        {
            // CreateGUI may not have been called yet.
            if (m_HelpBox == null || m_Container == null)
            {
                return;
            }

            m_HelpBox.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            m_Container.style.display = value ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void TryStartExecution()
        {
            if (m_GaussianMixtureModel != null)
            {
                m_GaussianMixtureModel.StartExecution();
            }
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.newRect.width * evt.newRect.height == 0)
            {
                return;
            }

            Utilities.AllocateIfNeededForCompute(ref m_Target, (int)evt.newRect.width, (int)evt.newRect.height,
                GraphicsFormat.R8G8B8A8_UNorm);
            m_Image.image = m_Target;
        }

        void SelectionChanged()
        {
            m_RenderHashcode = 0;

            var go = Selection.activeGameObject;
            if (go != null && go.TryGetComponent(typeof(GaussianMixtureModelComponent), out var gmm))
            {
                m_GaussianMixtureModel = (GaussianMixtureModelComponent)gmm;
                ShowHelpBox(false);
            }
            else
            {
                m_GaussianMixtureModel = null;
                ShowHelpBox(true);
            }
        }
    }
}
