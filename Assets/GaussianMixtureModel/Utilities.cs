using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GaussianMixtureModel
{
    public static class Utilities
    {
        public static float3 ToFloat3(this Color value)
        {
            return new float3(value.r, value.g, value.b);
        }

        public static void DeallocateIfNeeded(ref ComputeBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Dispose();
            buffer = null;
        }

        public static bool AllocateBufferIfNeeded<T>(
            ref ComputeBuffer buffer, int count,
            ComputeBufferType type = ComputeBufferType.Default, bool allowLarger = false) where T : struct
        {
            var stride = Marshal.SizeOf<T>();

            // We assume ComputeBufferType does not change for an already allocated buffer.
            var needsAlloc = buffer == null || (allowLarger ? buffer.count < count : buffer.count != count);

            if (needsAlloc)
            {
                DeallocateIfNeeded(ref buffer);
                buffer = new ComputeBuffer(count, stride, type);
                return true;
            }

            return false;
        }

        public static bool DeallocateNativeArrayIfNeeded<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
                return true;
            }

            return false;
        }

        public static bool AllocateNativeArrayIfNeeded<T>(
            ref NativeArray<T> array, int size, Allocator allocator = Allocator.Persistent) where T : struct
        {
            if (!array.IsCreated || array.Length != size)
            {
                DeallocateNativeArrayIfNeeded(ref array);

                array = new NativeArray<T>(size, allocator);
                return true;
            }

            return false;
        }

        public static void Destroy(Object obj)
        {
            if (obj == null)
                return;
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
                Object.Destroy(obj);
            else
#endif
                Object.DestroyImmediate(obj, true);
        }

        static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height, GraphicsFormat colorFormat,
            GraphicsFormat depthStencilFormat = GraphicsFormat.None)
        {
            // Note that we only check a subset of properties.
            if (rt == null ||
                rt.width != width ||
                rt.height != height ||
                rt.graphicsFormat != colorFormat ||
                rt.depthStencilFormat != depthStencilFormat)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(width, height, colorFormat, depthStencilFormat);
                return true;
            }

            return false;
        }

        public static bool AllocateIfNeededForCompute(ref RenderTexture rt, int width, int height,
            GraphicsFormat colorFormat, GraphicsFormat depthStencilFormat = GraphicsFormat.None)
        {
            if (AllocateIfNeeded(ref rt, width, height, colorFormat, depthStencilFormat))
            {
                // Compute shaders need random access write,
                // and must create the texture explicitly since it won't be bound as a graphics target before use.
                rt.enableRandomWrite = true;
                rt.Create();
                return true;
            }

            return false;
        }

        public static void DeallocateIfNeeded(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }

            rt = null;
        }

        // Relies on Indices fields name matching kernel names in the shader.
        public static void LoadKernelIndices<T>(ComputeShader shader, T indices)
        {
            foreach (var field in typeof(T).GetFields())
            {
                if (field.FieldType == typeof(int))
                {
                    var kernelId = shader.FindKernel(field.Name);
                    field.SetValue(indices, kernelId);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unexpected field type \"{field.Name}\" \"{field.FieldType}\"");
                }
            }
        }
    }
}