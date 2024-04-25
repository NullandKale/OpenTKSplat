using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace OpenTKSplat
{
    public class Quilt
    {
        BridgeInProc.Window window;

        public uint window_width = 0;
        public uint window_height = 0;
        public uint max_texture_size = 0;
        public int quilt_render_texture = 0;
        public int quilt_render_fbo = 0;
        public uint quilt_width = 0;
        public uint quilt_height = 0;
        public uint quilt_rows = 6;
        public uint quilt_cols = 6;
        public uint quilt_view_width = 0;
        public uint quilt_view_height = 0;

        public float viewAspect;
  
        public bool valid = true;
        
        public uint totalViews;

        public Quilt(BridgeInProc.Window bridge_window, uint viewScalar, uint vx = 6, uint vy = 6)
        {
            window = bridge_window;
            quilt_rows = vx;
            quilt_cols = vy;
            totalViews = quilt_rows * quilt_cols;

            // mlc: cache the size of the bridge output so we can decide how large to make
            // the quilt views
            BridgeInProc.Controller.WindowDimensions(window, ref window_width, ref window_height);

            // mlc: see how large we can make out render texture
            BridgeInProc.Controller.MaxTextureSize(window, ref max_texture_size);

            // mlc: now we need to figure out how large our views and quilt will be
            uint desired_view_width = window_width / viewScalar;
            uint desired_view_height = window_height / viewScalar;

            uint desired_render_texture_width = desired_view_width * quilt_rows;
            uint desired_render_texture_height = desired_view_height * quilt_cols;

            if (desired_render_texture_width <= max_texture_size &&
                desired_render_texture_height <= max_texture_size)
            {
                // mlc: under the max size -- good to go!
                quilt_view_width = desired_view_width;
                quilt_view_height = desired_view_height;
                quilt_width = desired_render_texture_width;
                quilt_height = desired_render_texture_height;
            }
            else
            {
                // mlc: the desired sizes are larger than we can support, find the dominant
                // and scale down to fit.
                float scalar = 0.0f;

                if (desired_render_texture_width > desired_render_texture_height)
                {
                    scalar = (float)max_texture_size / (float)desired_render_texture_width;
                }
                else
                {
                    scalar = (float)max_texture_size / (float)desired_render_texture_height;
                }

                quilt_view_width = (uint)((float)desired_view_width * scalar);
                quilt_view_height = (uint)((float)desired_view_height * scalar);
                quilt_width = (uint)((float)desired_render_texture_width * scalar);
                quilt_height = (uint)((float)desired_render_texture_height * scalar);
            }

            Console.WriteLine($"{quilt_rows}*{quilt_cols} @ {quilt_view_width}x{quilt_view_height} = quilt size: {quilt_width}x{quilt_height}");

            // mlc: generate the texture and fbo so we can interop!
            GL.GenTextures(1, out quilt_render_texture);
            GL.BindTexture(TextureTarget.Texture2D, quilt_render_texture);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgba,
                (int)quilt_width,
                (int)quilt_height,
                0, PixelFormat.Rgba, PixelType.UnsignedByte,
                IntPtr.Zero);

            // Framebuffer
            GL.GenFramebuffers(1, out quilt_render_fbo);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, quilt_render_fbo);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                quilt_render_texture,
                0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            if (window_width == 0 || window_width == 0)
            {
                Console.WriteLine("Bridge Error??");
                valid = false;
            }

            viewAspect = (quilt_view_width / (float)quilt_view_height);
        }


        private (Matrix4 viewMatrix, Matrix4 projectionMatrix) ComputeViewCameraData(Camera camera, int view, bool invert, float depthiness, float focus)
        {
            float tx = -(float)(quilt_rows * quilt_cols - 1) / 2.0f * depthiness + view * depthiness;
            float viewPosition = (float)view / (quilt_rows * quilt_cols);
            float centerPosition = 0.5f;
            float distanceFromCenter = viewPosition - centerPosition;
            float frustumShift = distanceFromCenter * focus;

            Matrix4 viewMatrix = camera.GetViewMatrix();
            Matrix4 translation = Matrix4.CreateTranslation(-tx, 0, 0);
            Matrix4 projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(camera.Fovy, camera.AspectRatio, camera.ZNear, camera.ZFar);

            // Apply inversion if needed
            if (invert)
            {
                Matrix4 invertView = Matrix4.CreateScale(1, -1, 1);
                translation *= invertView;
            }

            viewMatrix *= translation;
            projectionMatrix.M31 += frustumShift;

            return (viewMatrix, projectionMatrix);
        }

        private void SetViewport(int index)
        {
            // Calculate the position of the view within the quilt
            uint rowIndex = (uint)index / quilt_rows;
            uint columnIndex = (uint)index % quilt_rows;

            rowIndex = quilt_cols - 1 - rowIndex;

            // Calculate the viewport position
            int viewportX = (int)(columnIndex * quilt_view_width);
            int viewportY = (int)(rowIndex * quilt_view_height);

            // Set the OpenGL viewport to the calculated position and size
            GL.Viewport(viewportX, viewportY, (int)quilt_view_width, (int)quilt_view_height);
        }

        public void Draw(Action<Matrix4, Matrix4> render, Camera camera, float depthiness, float focus)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, quilt_render_fbo);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            for (int i = 0; i < totalViews; i++)
            {
                SetViewport(i);
                (Matrix4 viewMatrix, Matrix4 projectionMatrix) = ComputeViewCameraData(camera, i, true, depthiness, focus);
                render(viewMatrix, projectionMatrix);
            }

            BridgeInProc.Controller.DrawInteropQuiltTextureGL(
                window, (ulong)quilt_render_texture, BridgeInProc.PixelFormats.RGBA,
                quilt_width, quilt_height,
                quilt_rows, quilt_cols, viewAspect, 1.0f);
        }
    }
}