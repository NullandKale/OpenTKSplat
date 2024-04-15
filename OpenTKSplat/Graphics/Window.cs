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
        private FPSCounter fpsCounter;

        private Camera camera;
        private Shader shader;

        private int vertexBuffer;
        private int vao;
        private int elementBuffer;

        private GaussianData rawData;

        private VertexData[] gaussians;

        private PointCloudSorter sorter;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            fpsCounter = new FPSCounter();
            camera = new Camera(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y);

            string file = @"./Assets/gs_2020.ply";
            //string file = @"D:\Videos\Splats\Oblivion_Market_District_200_2.ply";
            //string file = @"D:\Videos\Splats\20221007_135905.ply";
            //string file = @"D:\Videos\Splats\20221007_135536.ply";
            //string file = @"D:\Videos\Splats\20231224_191934.ply";
            //string file = @"D:\Videos\Splats\20221025_155143.ply";
            //string file = @"D:\Videos\Splats\20221026_105130.ply";

            Console.WriteLine($"Loading {file}...");
            rawData = GaussianData.LoadPly(file);
            gaussians = rawData.Flatten();
            Console.WriteLine($"Loaded {gaussians.Length} splats");

            sorter = new PointCloudSorter(gaussians);
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0, 0, 0, 1.0f);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            shader = new Shader("Assets\\Shaders\\gau_vert.glsl", "Assets\\Shaders\\gau_frag.glsl");
            shader.Use();

            SetupGeometry();
            SetupGaussianData();

            shader.SetInt("sh_dim", 48);
            shader.SetFloat("scale_modifier", 1.0f);
            shader.SetInt("render_mod", 3);
        }

        private void SetupGeometry()
        {
            // Quad vertices and faces as per Python code
            float[] quadVertices = new float[] { -1, 1, 1, 1, 1, -1, -1, -1 };
            uint[] quadFaces = new uint[] { 0, 1, 2, 0, 2, 3 };

            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            elementBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, quadFaces.Length * sizeof(uint), quadFaces, BufferUsageHint.StaticDraw);

            // Get the location of the "position" attribute from the shader
            int positionLocation = GL.GetAttribLocation(shader.Handle, "position");

            // Set the vertex attribute pointers
            GL.VertexAttribPointer(positionLocation, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(positionLocation);

            // Unbind the VAO
            GL.BindVertexArray(0);
        }


        private void SetupGaussianData()
        {
            int gaussianSize = Unsafe.SizeOf<VertexData>();
            int totalSize = gaussians.Length * gaussianSize;

            // Generate a buffer for the gaussian data
            int gaussianBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, gaussianBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, totalSize, gaussians, BufferUsageHint.StaticDraw);

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
            camera.ProcessMouse(mouseState.X, mouseState.Y);

            // Process mouse buttons
            if (mouseState.IsButtonDown(MouseButton.Left))
            {
                camera.isLeftMousePressed = true;
            }
            else
            {
                camera.isLeftMousePressed = false;
            }

            if (mouseState.IsButtonDown(MouseButton.Right))
            {
                camera.isRightMousePressed = true;
            }
            else
            {
                camera.isRightMousePressed = false;
            }

            // Process mouse wheel
            camera.ProcessWheel(mouseState.ScrollDelta.X, mouseState.ScrollDelta.Y);

            // Process keyboard inputs for camera roll
            if (IsKeyDown(Keys.Q))
            {
                camera.ProcessRollKey(1);
            }
            else if (IsKeyDown(Keys.E))
            {
                camera.ProcessRollKey(-1);
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);

            camera.AspectRatio = e.Width / (float)e.Height;
            camera.UpdateResolution(e.Width, e.Height);

        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(vertexBuffer);
            GL.DeleteVertexArray(vao);
            shader.Dispose();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            shader.Use();
            GL.BindVertexArray(vao);

            Matrix4 viewMatrix = camera.GetViewMatrix();

            if (camera.isPoseDirty)
            {
                shader.SetMatrix4("view_matrix", viewMatrix);
                shader.SetVector3("cam_pos", camera.Position);
                camera.isPoseDirty = false;
            }

            if (camera.isIntrinDirty)
            {
                Matrix4 projectionMatrix = camera.GetProjectionMatrix();
                shader.SetMatrix4("projection_matrix", projectionMatrix);
                shader.SetVector3("hfovxy_focal", camera.GetHtanFovxyFocal());
                camera.isIntrinDirty = false;
            }

            sorter.sort(viewMatrix);

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, sorter.cudaGlInteropIndexBuffer.glBufferHandle);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0, gaussians.Length);

            SwapBuffers();

            fpsCounter.Update(args.Time);
        }
    }
}
