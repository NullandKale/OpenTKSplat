using OpenTKSplat.Data;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Algorithms.RadixSortOperations;
using System.Runtime.InteropServices;
using ILGPU.Runtime.OpenCL;
using OpenTK.Mathematics;
using OpenTKSplat.Compute;

namespace OpenTKSplat.Kinect
{
    public class PointCloudSorter : IDisposable
    {
        public float[] cpu_screen_space_depth_buffer;
        public int[] cpu_particle_index_buffer;

        private GCHandle indexBufferHandle;

        public MemoryBuffer1D<VertexData, Stride1D.Dense> gpu_particlesMerged;
        public MemoryBuffer1D<float, Stride1D.Dense> gpu_screen_space_depth;
        public CudaGlInteropIndexBuffer cudaGlInteropIndexBuffer;

        public MemoryBuffer1D<int, Stride1D.Dense> sortTmpBuffer;
        public RadixSortPairs<float, Stride1D.Dense, int, Stride1D.Dense> radixSort;
        
        public Context context;
        public Accelerator gpu;

        public Action<Index1D, ArrayView1D<float, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<VertexData, Stride1D.Dense>, Matrix4> particlePacker;

        public PointCloudSorter(VertexData[] vertexData)
        {
            context = Context.Create(builder => builder.CPU().Cuda().
                                            EnableAlgorithms().
                                            Math(MathMode.Fast32BitOnly).
                                            Inlining(InliningMode.Aggressive).
                                            AutoAssertions().
                                            Optimize(OptimizationLevel.O2));
            gpu = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);
            
            particlePacker = gpu.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<float, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<VertexData, Stride1D.Dense>, Matrix4>(ParticlePackingKernel);

            SetVertexData(vertexData);
        }

        public void SetVertexData(VertexData[] vertexData)
        {
            if(indexBufferHandle.IsAllocated)
            {
                DisposeBuffers();
            }

            cpu_screen_space_depth_buffer = new float[vertexData.Length];
            cpu_particle_index_buffer = new int[vertexData.Length];
            indexBufferHandle = GCHandle.Alloc(cpu_particle_index_buffer, GCHandleType.Pinned);

            gpu_screen_space_depth = gpu.Allocate1D<float>(vertexData.Length);
            cudaGlInteropIndexBuffer = new CudaGlInteropIndexBuffer(vertexData.Length, gpu as CudaAccelerator);
            gpu_particlesMerged = gpu.Allocate1D(vertexData);

            radixSort = gpu.CreateRadixSortPairs<float, Stride1D.Dense, int, Stride1D.Dense, DescendingFloat>();
            Index1D tempMemSize = gpu.ComputeRadixSortPairsTempStorageSize<float, Stride1D.Dense, DescendingFloat>(vertexData.Length);
            sortTmpBuffer = gpu.Allocate1D<int>(tempMemSize);
        }

        private static void ParticlePackingKernel(Index1D index, ArrayView1D<float, Stride1D.Dense> depths, ArrayView1D<int, Stride1D.Dense> indices, ArrayView1D<VertexData, Stride1D.Dense> particles, Matrix4 viewMatrix)
        {
            Vector3 position = particles[index].Position;
            Vector3 rotatedPosition = new Vector3(
                Vector3.Dot(position, new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31)),
                Vector3.Dot(position, new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32)),
                Vector3.Dot(position, new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33))
            );

            depths[index] = rotatedPosition.Z + viewMatrix.M43;
            indices[index] = index;
        }

        public void sort(Matrix4 viewCamera)
        {
            cudaGlInteropIndexBuffer.MapCuda(gpu.DefaultStream as CudaStream);

            var gpuParticleIndexView = cudaGlInteropIndexBuffer.GetCudaArrayView();

            particlePacker((int)gpu_particlesMerged.Length, gpu_screen_space_depth, gpuParticleIndexView, gpu_particlesMerged, viewCamera);
            radixSort(gpu.DefaultStream, gpu_screen_space_depth, gpuParticleIndexView, sortTmpBuffer.View);

            gpu.Synchronize();

            cudaGlInteropIndexBuffer.UnmapCuda(gpu.DefaultStream as CudaStream);

            gpu.Synchronize();
        }

        public void DisposeBuffers()
        {
            gpu_screen_space_depth.Dispose();
            cudaGlInteropIndexBuffer.Dispose();
            gpu_particlesMerged.Dispose();
            sortTmpBuffer.Dispose();

            // Release the pinned memory
            if (indexBufferHandle.IsAllocated)
            {
                indexBufferHandle.Free();
            }
        }

        public void Dispose()
        {
            DisposeBuffers();

            gpu.Dispose();
            context.Dispose();
        }

    }
}