using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace OpenTKSplat
{
    public class Quilt
    {
        private BridgeInProc.Window window;

        public uint LKGDisplayWidth = 0;
        public uint LKGDisplayHeight = 0;
        
        public int QuiltRenderTextureID = 0;
        public int QuiltRenderFBO = 0;

        public uint QuiltWidth = 0;
        public uint QuiltHeight = 0;
        
        public uint TileCountX = 6;
        public uint TileCountY = 6;
        public uint TileCount = 0;

        public uint TileWidth = 0;
        public uint TileHeight = 0;

        public float DesiredViewAspect = 0;

        public bool IsValid = false;

        public Quilt(BridgeInProc.Window bridge_window, uint viewScalar, uint vx = 6, uint vy = 6)
        {
            window = bridge_window;
            TileCountX = vx;
            TileCountY = vy;
            TileCount = TileCountX * TileCountY;

            // mlc: cache the size of the bridge output so we can decide how large to make
            // the quilt views
            BridgeInProc.Controller.GetWindowDimensions(window, ref LKGDisplayWidth, ref LKGDisplayHeight);

            uint GPUMaxTextureSize = 0;

            // mlc: see how large we can make out render texture
            BridgeInProc.Controller.GetMaxTextureSize(window, ref GPUMaxTextureSize);

            // mlc: now we need to figure out how large our views and quilt will be
            uint desired_view_width = LKGDisplayWidth / viewScalar;
            uint desired_view_height = LKGDisplayHeight / viewScalar;

            uint desired_render_texture_width = desired_view_width * TileCountX;
            uint desired_render_texture_height = desired_view_height * TileCountY;

            if (desired_render_texture_width <= GPUMaxTextureSize &&
                desired_render_texture_height <= GPUMaxTextureSize)
            {
                // mlc: under the max size -- good to go!
                TileWidth = desired_view_width;
                TileHeight = desired_view_height;
                QuiltWidth = desired_render_texture_width;
                QuiltHeight = desired_render_texture_height;
            }
            else
            {
                // mlc: the desired sizes are larger than we can support, find the dominant
                // and scale down to fit.
                float scalar;

                if (desired_render_texture_width > desired_render_texture_height)
                {
                    scalar = (float)GPUMaxTextureSize / (float)desired_render_texture_width;
                }
                else
                {
                    scalar = (float)GPUMaxTextureSize / (float)desired_render_texture_height;
                }

                TileWidth = (uint)((float)desired_view_width * scalar);
                TileHeight = (uint)((float)desired_view_height * scalar);
                QuiltWidth = (uint)((float)desired_render_texture_width * scalar);
                QuiltHeight = (uint)((float)desired_render_texture_height * scalar);
            }

            Console.WriteLine($"{TileCountX}*{TileCountY} @ {TileWidth}x{TileHeight} = quilt size: {QuiltWidth}x{QuiltHeight}");

            // mlc: generate the texture and fbo so we can interop!
            GL.GenTextures(1, out QuiltRenderTextureID);
            GL.BindTexture(TextureTarget.Texture2D, QuiltRenderTextureID);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgba,
                (int)QuiltWidth,
                (int)QuiltHeight,
                0, PixelFormat.Rgba, PixelType.UnsignedByte,
                IntPtr.Zero);

            // Framebuffer
            GL.GenFramebuffers(1, out QuiltRenderFBO);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, QuiltRenderFBO);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                QuiltRenderTextureID,
                0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            IsValid = true;

            if (LKGDisplayWidth == 0 || LKGDisplayWidth == 0)
            {
                Console.WriteLine("Bridge Error??");
                IsValid = false;
            }

            DesiredViewAspect = (TileWidth / (float)TileHeight);
        }

        private (Matrix4 viewMatrix, Matrix4 projectionMatrix) ComputeViewCameraData(Camera camera, int view, bool invert, float depthiness, float focus)
        {
            float tx = -(float)(TileCountX * TileCountY - 1) / 2.0f * depthiness + view * depthiness;
            float viewPosition = (float)view / (TileCountX * TileCountY);
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
            uint rowIndex = (uint)index / TileCountX;
            uint columnIndex = (uint)index % TileCountX;

            rowIndex = TileCountY - 1 - rowIndex;

            // Calculate the viewport position
            int viewportX = (int)(columnIndex * TileWidth);
            int viewportY = (int)(rowIndex * TileHeight);

            // Set the OpenGL viewport to the calculated position and size
            GL.Viewport(viewportX, viewportY, (int)TileWidth, (int)TileHeight);
        }

        public void Save(string filenamePrefix)
        {
            string filenamePrefixWithoutExtension = Path.GetFileNameWithoutExtension(filenamePrefix);
            string filenamePostfix = $"_qs{TileCountX}x{TileCountY}a{DesiredViewAspect}z1.png";
            string filename = filenamePrefixWithoutExtension + filenamePostfix;

            // Call the method to save the texture to file
            BridgeInProc.Controller.SaveTextureToFileGL(
                window,
                filename,
                (ulong)QuiltRenderTextureID,
                BridgeInProc.PixelFormats.BGRA,
                QuiltWidth,
                QuiltHeight
            );
        }



        public void Draw(Action<Matrix4, Matrix4> render, Camera camera, float depthiness, float focus)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, QuiltRenderFBO);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            for (int i = 0; i < TileCount; i++)
            {
                SetViewport(i);
                (Matrix4 viewMatrix, Matrix4 projectionMatrix) = ComputeViewCameraData(camera, i, true, depthiness, focus);
                render(viewMatrix, projectionMatrix);
            }

            BridgeInProc.Controller.DrawInteropQuiltTextureGL(
                window, (ulong)QuiltRenderTextureID, BridgeInProc.PixelFormats.RGBA,
                QuiltWidth, QuiltHeight,
                TileCountX, TileCountY, DesiredViewAspect, 1.0f);
        }

    }
}