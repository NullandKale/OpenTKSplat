using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTKSplat.Graphics;
using OpenTKSplat.Data;
using System.Runtime.CompilerServices;
using OpenTKSplat.Kinect;
using System.Diagnostics;

namespace OpenTKSplat
{
    public class Window : GameWindow
    {
        private Stopwatch _timer = new Stopwatch();
        private Queue<double> _fpsValues = new Queue<double>();
        private double _fpsSum = 0;
        private double _printFpsInterval = 5000; // 5000 milliseconds = 5 seconds
        private double _lastPrintTime = 0;

        private Camera _camera;
        private Shader _shader;

        private int _vertexBuffer;
        private int _vao;
        private int _elementBuffer;

        private GaussianData _rawData;
        private VertexData[] _gaussians;

        PointCloudSorter sorter;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            _camera = new Camera(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y);

            //string file = @"C:\Users\alec\Downloads\2020_gaussian_splatting_point_cloud.ply\gs_2020.ply";
            string file = @"D:\Videos\Splats\Oblivion_Market_District_200_2.ply";

            Console.WriteLine($"Loading {file}...");
            _rawData = GaussianData.LoadPly(file);
            _gaussians = _rawData.Flatten();
            Console.WriteLine($"Loaded {_gaussians.Length} splats");

            sorter = new PointCloudSorter(_gaussians);
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0, 0, 0, 1.0f);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _shader = new Shader("Assets\\Shaders\\gau_vert.glsl", "Assets\\Shaders\\gau_frag.glsl");
            _shader.Use();

            SetupGeometry();
            SetupGaussianData();

            _shader.SetInt("sh_dim", VertexData.SphericalHarmonicsLength);
            _shader.SetFloat("scale_modifier", 1.0f);
            _shader.SetInt("render_mod", 3);

            _timer.Start();
        }

        private void SetupGeometry()
        {
            // Quad vertices and faces as per Python code
            float[] quadVertices = new float[] { -1, 1, 1, 1, 1, -1, -1, -1 };
            uint[] quadFaces = new uint[] { 0, 1, 2, 0, 2, 3 };

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            _elementBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, quadFaces.Length * sizeof(uint), quadFaces, BufferUsageHint.StaticDraw);

            // Get the location of the "position" attribute from the shader
            int positionLocation = GL.GetAttribLocation(_shader.Handle, "position");

            // Set the vertex attribute pointers
            GL.VertexAttribPointer(positionLocation, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(positionLocation);

            // Unbind the VAO
            GL.BindVertexArray(0);
        }


        private void SetupGaussianData()
        {
            int gaussianSize = Unsafe.SizeOf<VertexData>();
            int totalSize = _gaussians.Length * gaussianSize;

            // Generate a buffer for the gaussian data
            int gaussianBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, gaussianBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, totalSize, _gaussians, BufferUsageHint.StaticDraw);

            // Bind this buffer to a binding point, e.g., 0
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, gaussianBuffer);

            // Set up vertex attributes for Gaussian data
            // Position
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, gaussianSize, 0);
            GL.EnableVertexAttribArray(0);

            // Rotation
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, gaussianSize, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Scale
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, gaussianSize, 7 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // Opacity
            GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, gaussianSize, 10 * sizeof(float));
            GL.EnableVertexAttribArray(3);

            // Spherical Harmonics
            for (int i = 0; i < 48; ++i)
            {
                GL.VertexAttribPointer(4 + i, 1, VertexAttribPointerType.Float, false, gaussianSize, 11 * sizeof(float) + i * sizeof(float));
                GL.EnableVertexAttribArray(4 + i);
            }
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            if (IsKeyDown(Keys.Escape))
            {
                Close();
            }

            // Process mouse movement
            var mouseState = MouseState;
            _camera.ProcessMouse(mouseState.X, mouseState.Y);

            // Process mouse buttons
            if (mouseState.IsButtonDown(MouseButton.Left))
            {
                _camera.isLeftMousePressed = true;
            }
            else
            {
                _camera.isLeftMousePressed = false;
            }

            if (mouseState.IsButtonDown(MouseButton.Right))
            {
                _camera.isRightMousePressed = true;
            }
            else
            {
                _camera.isRightMousePressed = false;
            }

            // Process mouse wheel
            _camera.ProcessWheel(mouseState.ScrollDelta.X, mouseState.ScrollDelta.Y);

            // Process keyboard inputs for camera roll
            if (IsKeyDown(Keys.Q))
            {
                _camera.ProcessRollKey(1);
            }
            else if (IsKeyDown(Keys.E))
            {
                _camera.ProcessRollKey(-1);
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);

            _camera.AspectRatio = e.Width / (float)e.Height;
            _camera.UpdateResolution(e.Width, e.Height);

        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(_vertexBuffer);
            GL.DeleteVertexArray(_vao);
            _shader.Dispose();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            _shader.Use();
            GL.BindVertexArray(_vao);

            Matrix4 viewMatrix = _camera.GetViewMatrix();

            if (_camera.isPoseDirty)
            {
                _shader.SetMatrix4("view_matrix", viewMatrix);
                _shader.SetVector3("cam_pos", _camera.Position);
                _camera.isPoseDirty = false;
            }

            if (_camera.isIntrinDirty)
            {
                Matrix4 projectionMatrix = _camera.GetProjectionMatrix();
                _shader.SetMatrix4("projection_matrix", projectionMatrix);
                _shader.SetVector3("hfovxy_focal", _camera.GetHtanFovxyFocal());
                _camera.isIntrinDirty = false;
            }

            sorter.sort(viewMatrix);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, sorter.cudaGlInteropIndexBuffer._glBufferHandle);
            //GL.BufferData(BufferTarget.ShaderStorageBuffer, _gaussians.Length * sizeof(int), sorter.cpu_particle_index_buffer, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, sorter.cudaGlInteropIndexBuffer._glBufferHandle);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0, _gaussians.Length);

            SwapBuffers();

            // Calculate FPS for the current frame
            double fps = 1.0 / args.Time;
            _fpsSum += fps;
            _fpsValues.Enqueue(fps);

            // Keep the sliding window to the last 10 seconds
            while (_fpsValues.Count > 0 && _timer.ElapsedMilliseconds - _lastPrintTime > 10000) // 10 seconds
            {
                _fpsSum -= _fpsValues.Dequeue();
            }

            // Print average FPS every 5 seconds
            if (_timer.ElapsedMilliseconds - _lastPrintTime >= _printFpsInterval)
            {
                double averageFps = _fpsValues.Count > 0 ? _fpsSum / _fpsValues.Count : 0;
                Console.WriteLine($"Average FPS over the last 10 seconds: {averageFps:F2}");
                _lastPrintTime = _timer.ElapsedMilliseconds;
            }

        }
    }
}
