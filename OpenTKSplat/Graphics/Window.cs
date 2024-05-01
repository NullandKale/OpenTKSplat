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
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using OpenTK.Compute.OpenCL;
using System.Drawing;

namespace OpenTKSplat
{
    public class Window : GameWindow
    {
        private FPSCounter fpsCounter;

        private Camera camera;
        private Shader shader;

        public BridgeInProc.Window bridge_window;
        public Quilt quilt;

        private int vertexBuffer;
        private int vao;
        private int elementBuffer;

        private GaussianData rawData;

        private GaussianSplat[] gaussians;

        private PointCloudSorter sorter;

        private float depthiness = 0.02f;
        private float depthinessDelta = 0.001f;

        private float focus = 0;
        private float focusDelta = 0.1f;

        private bool sortOnce = true;
        private bool render3D = true;

        private uint quiltScale = 4;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            fpsCounter = new FPSCounter();
            camera = new Camera(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y);
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

            shader.SetFloat("scale_modifier", 1.0f);

            string file = @"./Assets/gs_2020.ply";
            LoadAndSetupPlyFile(file);

            try
            {
                if(render3D)
                {
                    // Initialize bridge
                    BridgeInProc.Controller.Initialize();

                    // Instance the window on the looking glass display
                    if (BridgeInProc.Controller.InstanceWindowGL(ref bridge_window, true))
                    {
                        // allocate a framebuffer for the window
                        quilt = new Quilt(bridge_window, quiltScale, 8, 6);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Failed to connect to Looking Glass display, rendering 2D only");
            }

            if (quilt != null && quilt.valid)
            {
                Size = new Vector2i((int)quilt.window_width / (int)quiltScale, (int)quilt.window_height / (int)quiltScale);
            }

            FileDrop += OnFileDrop;
        }

        private void OnFileDrop(FileDropEventArgs e)
        {
            foreach (string file in e.FileNames)
            {
                if (file.EndsWith(".ply"))
                {
                    LoadAndSetupPlyFile(file);
                }
                else
                {
                    Console.WriteLine($"Unsupported file format: {file}");
                }
            }
        }

        private void LoadAndSetupPlyFile(string file)
        {
            Console.WriteLine($"Loading {file}...");
            rawData = GaussianData.LoadPly(file);
            gaussians = rawData.Flatten();
            Console.WriteLine($"Loaded {gaussians.Length} splats");

            if(sorter != null)
            {
                sorter.Dispose();
            }

            sorter = new PointCloudSorter(rawData.Positions);

            SetupGeometry();
            SetupGaussianData();
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
            int gaussianSize = Unsafe.SizeOf<GaussianSplat>();
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

            // Check for left or right arrow keys to adjust depthiness
            if (IsKeyDown(Keys.Left))
            {
                depthiness -= depthinessDelta;
            }
            else if (IsKeyDown(Keys.Right))
            {
                depthiness += depthinessDelta;
            }

            // Check for up or down arrow keys to adjust focus
            if (IsKeyDown(Keys.Up))
            {
                focus += focusDelta;
            }
            else if (IsKeyDown(Keys.Down))
            {
                focus -= focusDelta;
            }

            camera.ProcessInputs(MouseState, KeyboardState);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);

            camera.AspectRatio = e.Width / (float)e.Height;
            camera.UpdateResolution(e.Width, e.Height);

            shader.SetVector3("hfovxy_focal", camera.GetHtanFovxyFocal());
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(vertexBuffer);
            GL.DeleteVertexArray(vao);

            shader.Dispose();
        }


        private void render(Matrix4 viewMatrix, Matrix4 projectionMatrix)
        {
            shader.SetMatrix4("view_matrix", viewMatrix);
            shader.SetVector3("cam_pos", camera.Position);
            shader.SetMatrix4("projection_matrix", projectionMatrix);

            if(!sortOnce)
            {
                sorter.sort(viewMatrix);
            }

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, sorter.cudaGlInteropIndexBuffer.glBufferHandle);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0, gaussians.Length);

        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            Matrix4 viewMatrix;
            Matrix4 projectionMatrix;

            if(sortOnce)
            {
                viewMatrix = camera.GetViewMatrix();
                sorter.sort(viewMatrix);
            }

            shader.Use();
            GL.BindVertexArray(vao);

            // draw 3d
            if (quilt != null && quilt.valid)
            {
                quilt.Draw(render, camera, depthiness, focus);
            }

            // draw 2d
            viewMatrix = camera.GetViewMatrix();
            projectionMatrix = camera.GetProjectionMatrix();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Viewport(0, 0, Size.X, Size.Y);

            render(viewMatrix, projectionMatrix);

            SwapBuffers();

            fpsCounter.Update(args.Time);
        }
    }
}
