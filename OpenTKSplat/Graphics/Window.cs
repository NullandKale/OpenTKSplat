using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTKSplat.Graphics;
using OpenTKSplat.Data;
using System.Runtime.CompilerServices;

namespace OpenTKSplat
{
    public class Window : GameWindow
    {
        private Camera _camera;
        private Shader _shader;

        private int _vertexBuffer;
        private int _vao;
        private int _elementBuffer;

        private GaussianData _rawData;
        private VertexData[] _gaussians;
        private float[] _gaussianDepths;
        private int[] _gaussianIndices;
        private int _indexBuffer;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            _camera = new Camera(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y);

            string file = "point_cloud.ply";

            _rawData = GaussianData.LoadPly(file);
            _gaussians = _rawData.Flatten();

            _gaussianDepths = new float[_gaussians.Length];
            _gaussianIndices = new int[_gaussians.Length];
            for (int i = 0; i < _gaussians.Length; i++)
            {
                _gaussianIndices[i] = i;
            }

            // Initialize index buffer
            _indexBuffer = GL.GenBuffer();
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


            if (_camera.isPoseDirty)
            {
                Matrix4 viewMatrix = _camera.GetViewMatrix();
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

            SortGaussiansParallel();

            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0, _gaussians.Length);

            SwapBuffers();
        }

        private void SortGaussians()
        {
            Matrix4 viewMatrix = _camera.GetViewMatrix();

            // Calculate depths
            for (int i = 0; i < _gaussians.Length; i++)
            {
                Vector3 position = _gaussians[i].Position;
                Vector3 rotatedPosition = new Vector3(
                    Vector3.Dot(position, new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31)),
                    Vector3.Dot(position, new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32)),
                    Vector3.Dot(position, new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33))
                );
                _gaussianDepths[i] = rotatedPosition.Z + viewMatrix.M43; // Z component after applying rotation and translation
                _gaussianIndices[i] = i;
            }

            // Use Array.Sort to sort the indices array based on the depths array
            Array.Sort(_gaussianDepths, _gaussianIndices);

            // Update buffer with sorted indices
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _indexBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, _gaussianIndices.Length * sizeof(int), _gaussianIndices, BufferUsageHint.DynamicDraw);

            // Bind buffer to the binding index
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _indexBuffer);
        }

        private void SortGaussiansParallel()
        {
            Matrix4 viewMatrix = _camera.GetViewMatrix();

            int coreCount = 4;
            int chunkSize = _gaussians.Length / coreCount;
            int leftovers = _gaussians.Length % coreCount;

            // Process the chunks in parallel
            Parallel.For(0, coreCount, core =>
            {
                int start = core * chunkSize;
                int end = (core + 1) * chunkSize;

                for (int i = start; i < end; i++)
                {
                    Vector3 position = _gaussians[i].Position;
                    Vector3 rotatedPosition = new Vector3(
                        Vector3.Dot(position, new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31)),
                        Vector3.Dot(position, new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32)),
                        Vector3.Dot(position, new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33))
                    );
                    _gaussianDepths[i] = rotatedPosition.Z + viewMatrix.M43; // Z component after applying rotation and translation
                    _gaussianIndices[i] = i;
                }
            });

            // Process any leftovers
            Parallel.For(_gaussians.Length - leftovers, _gaussians.Length, i =>
            {
                Vector3 position = _gaussians[i].Position;
                Vector3 rotatedPosition = new Vector3(
                    Vector3.Dot(position, new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31)),
                    Vector3.Dot(position, new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32)),
                    Vector3.Dot(position, new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33))
                );
                _gaussianDepths[i] = rotatedPosition.Z + viewMatrix.M43; // Z component after applying rotation and translation
                _gaussianIndices[i] = i;
            });


            // Use Array.Sort to sort the indices array based on the depths array
            Array.Sort(_gaussianDepths, _gaussianIndices);

            // Update buffer with sorted indices
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _indexBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, _gaussianIndices.Length * sizeof(int), _gaussianIndices, BufferUsageHint.DynamicDraw);

            // Bind buffer to the binding index
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _indexBuffer);
        }
    }
}
