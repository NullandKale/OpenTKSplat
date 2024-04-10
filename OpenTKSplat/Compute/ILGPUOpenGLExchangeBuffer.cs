using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using OpenTK.Mathematics;
using OpenTKSplat.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace OpenTKSplat.Compute
{
    public enum CudaGraphicsMapFlags
    {
        None = 0,
        ReadOnly = 1,
        WriteDiscard = 2
    }

    public static class CudaGlInterop
    {
        [DllImport("nvcuda", EntryPoint = "cuGraphicsGLRegisterBuffer")]
        public static extern CudaError RegisterBuffer(
            out IntPtr resource,
            int buffer,
            uint flags);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsMapResources")]
        public static extern CudaError MapResources(
            int count,
            IntPtr resources,
            IntPtr stream);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsUnmapResources")]
        public static extern CudaError UnmapResources(
            int count,
            IntPtr resources,
            IntPtr stream);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsResourceGetMappedPointer_v2")]
        public static extern CudaError GetMappedPointer(
            out IntPtr devicePtr,
            out int size,
            IntPtr resource);
    }


    /// <summary>
    ///     A 2D floating point buffer that can be read from + written to by both OpenGL and CUDA
    /// </summary>
    public sealed class CudaGlInteropIndexBuffer : MemoryBuffer
    {
        private IntPtr _cudaResource;
        public readonly int _glBufferHandle;
        private State _state;
        private readonly int _elementCount;

        public CudaGlInteropIndexBuffer(int elementCount, CudaAccelerator accelerator)
            : base(accelerator, elementCount * sizeof(int), sizeof(int))
        {
            _elementCount = elementCount;

            // Create the OpenGL buffer (index buffer in this case)
            _glBufferHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _glBufferHandle);
            GL.BufferData(BufferTarget.ElementArrayBuffer, elementCount * sizeof(int), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

            // Register the OpenGL buffer with CUDA
            CudaException.ThrowIfFailed(CudaGlInterop.RegisterBuffer(
                out _cudaResource,
                _glBufferHandle,
                (int)CudaGraphicsMapFlags.None)); // None => CUDA can both read and write the buffer

            _state = State.AvailableForGl;
        }

        public void MapCuda(CudaStream stream)
        {
            if (_state == State.AvailableForGl)
            {
                unsafe
                {
                    fixed (IntPtr* pResources = &_cudaResource)
                    {
                        CudaException.ThrowIfFailed(CudaGlInterop.MapResources(
                            1, new IntPtr(pResources), stream.StreamPtr));
                    }
                }

                _state = State.MappedToCuda;
            }
        }

        public ArrayView<int> GetCudaArrayView()
        {
            if (_state != State.MappedToCuda)
                throw new InvalidOperationException("Buffer must be mapped to CUDA before accessing.");

            CudaException.ThrowIfFailed(CudaGlInterop.GetMappedPointer(
                out var devicePtr, out var bufLen, _cudaResource));
            Trace.Assert(bufLen == _elementCount * sizeof(int));
            NativePtr = devicePtr;

            var view = AsArrayView<int>(0, _elementCount);
            return view;
        }

        public void UnmapCuda(CudaStream stream)
        {
            if (_state == State.MappedToCuda)
            {
                unsafe
                {
                    fixed (IntPtr* pResources = &_cudaResource)
                    {
                        CudaException.ThrowIfFailed(CudaGlInterop.UnmapResources(
                            1, new IntPtr(pResources), stream.StreamPtr));
                    }
                }

                NativePtr = IntPtr.Zero;
                _state = State.AvailableForGl;
            }
        }

        public static void CudaMemSet<T>(CudaStream stream, byte value, in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (targetView.GetAcceleratorType() != AcceleratorType.Cuda) throw new NotSupportedException();

            var binding = stream.Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CudaAPI.CurrentAPI.Memset(
                    targetView.LoadEffectiveAddressAsPtr(),
                    value,
                    new IntPtr(targetView.LengthInBytes),
                    stream));

            binding.Recover();
        }

        public static void CudaCopy<T>(CudaStream stream, in ArrayView<T> sourceView, in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using var binding = stream.Accelerator.BindScoped();

            var sourceType = sourceView.GetAcceleratorType();
            var targetType = targetView.GetAcceleratorType();

            if (sourceType == AcceleratorType.OpenCL ||
                targetType == AcceleratorType.OpenCL)
                throw new NotSupportedException();

            var sourceAddress = sourceView.LoadEffectiveAddressAsPtr();
            var targetAddress = targetView.LoadEffectiveAddressAsPtr();

            var length = new IntPtr(targetView.LengthInBytes);

            // a) Copy from CPU to GPU
            // b) Copy from GPU to CPU
            // c) Copy from GPU to GPU
            CudaException.ThrowIfFailed(
                CudaAPI.CurrentAPI.MemcpyAsync(
                    targetAddress,
                    sourceAddress,
                    length,
                    stream));
        }


        protected override void MemSet(
            AcceleratorStream stream,
            byte value,
            in ArrayView<byte> targetView)
        {
            CudaMemSet(stream as CudaStream, value, targetView);
        }

        protected override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView)
        {
            CudaCopy(stream as CudaStream, sourceView, targetView);
        }

        protected override void CopyTo(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView)
        {
            CudaCopy(stream as CudaStream, sourceView, targetView);
        }

        protected override void DisposeAcceleratorObject(bool disposing)
        {
            // Unregister the CUDA resource
            // Delete the gl texture
            // Delete the gl pbo
            throw new NotImplementedException();
        }

        private enum State
        {
            AvailableForGl,
            MappedToCuda
        }
    }
}
