using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace GaussianMixtureModel
{
    static class RenderDocCaptureScopeFactory
    {
#if UNITY_EDITOR
        class RenderDocCaptureScope : IDisposable
        {
            static readonly Type k_GameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");

            EditorWindow m_Window;

            public RenderDocCaptureScope()
            {
                m_Window = EditorWindow.GetWindow(k_GameViewType);

                if (IsValid())
                {
                    RenderDoc.BeginCaptureRenderDoc(m_Window);
                }
            }

            public void Dispose()
            {
                if (IsValid())
                {
                    RenderDoc.EndCaptureRenderDoc(m_Window);
                }
            }

            bool IsValid() => m_Window != null && RenderDoc.IsLoaded() && RenderDoc.IsSupported();
        }

        public static IDisposable Create() => new RenderDocCaptureScope();
#else
        class NullScope : IDisposable
        {
            public void Dispose(){}
        }
        public static IDisposable Create() => new NullScope();
#endif
    }
}