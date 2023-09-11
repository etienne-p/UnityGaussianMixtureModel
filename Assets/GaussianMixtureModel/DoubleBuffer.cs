using UnityEngine;

namespace GaussianMixtureModel
{
    class DoubleBuffer<T> where T : struct
    {
        ComputeBuffer m_BufferIn;
        ComputeBuffer m_BufferOut;

        public ComputeBuffer In => m_BufferIn;
        public ComputeBuffer Out => m_BufferOut;

        public void AllocateIfNeeded(int size)
        {
            Utilities.AllocateBufferIfNeeded<T>(ref m_BufferIn, size);
            Utilities.AllocateBufferIfNeeded<T>(ref m_BufferOut, size);
        }

        public void Dispose()
        {
            Utilities.DeallocateIfNeeded(ref m_BufferIn);
            Utilities.DeallocateIfNeeded(ref m_BufferOut);
        }

        public void Swap() => (m_BufferIn, m_BufferOut) = (m_BufferOut, m_BufferIn);
    }
}
