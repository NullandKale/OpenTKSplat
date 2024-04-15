using OpenTK.Mathematics;

namespace OpenTKSplat
{
    public class Camera
    {
        public float AspectRatio { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Target { get; private set; }
        public Vector3 Up { get; private set; }
        private float Fovy;
        private float ZNear;
        private float ZFar;
        private int Width;
        private int Height;
        private float Yaw;
        private float Pitch;
        private float LastX;
        private float LastY;
        private bool FirstMouse;
        public bool isPoseDirty;
        public bool isIntrinDirty;
        public bool isLeftMousePressed;
        public bool isRightMousePressed;
        private float RotSensitivity;
        private float TransSensitivity;
        private float ZoomSensitivity;
        private float RollSensitivity;

        public Camera(int height, int width)
        {
            ZNear = 0.01f;
            ZFar = 1000f;
            Height = height;
            Width = width;
            Fovy = MathF.PI / 3;
            Position = new Vector3(0f, 0f, 3f);
            Target = new Vector3(0f, 0f, 0f);
            Up = new Vector3(0f, -1f, 0f);
            Yaw = -MathF.PI / 2;
            Pitch = 0f;

            isPoseDirty = true;
            isIntrinDirty = true;

            LastX = 640f;
            LastY = 360f;
            FirstMouse = true;

            isLeftMousePressed = false;
            isRightMousePressed = false;

            RotSensitivity = 0.02f;
            TransSensitivity = 0.01f;
            ZoomSensitivity = 0.08f;
            RollSensitivity = 0.03f;
        }

        private Matrix4 GlobalRotMat()
        {
            Vector3 x = new Vector3(1f, 0f, 0f);
            Vector3 z = Vector3.Cross(x, Up);
            z.Normalize();
            x = Vector3.Cross(Up, z);
            return new Matrix4(new Vector4(x, 0), new Vector4(Up, 0), new Vector4(z, 0), Vector4.UnitW);
        }

        internal Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Target, Up);
        }

        internal Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(Fovy, Width / (float)Height, ZNear, ZFar);
        }

        internal Vector3 GetHtanFovxyFocal()
        {
            float htany = MathF.Tan(Fovy / 2);
            float htanx = htany / Height * Width;
            float focal = Height / (2 * htany);
            return new Vector3(htanx, htany, focal);
        }

        internal void ProcessMouse(float xpos, float ypos)
        {
            if (FirstMouse)
            {
                LastX = xpos;
                LastY = ypos;
                FirstMouse = false;
            }

            float xoffset = xpos - LastX;
            float yoffset = LastY - ypos;
            LastX = xpos;
            LastY = ypos;

            if (isLeftMousePressed)
            {
                Yaw += xoffset * RotSensitivity;
                Pitch += yoffset * RotSensitivity;

                Pitch = Math.Clamp(Pitch, -MathF.PI / 2, MathF.PI / 2);

                Matrix4 rotMat = GlobalRotMat();
                Vector3 front = new Vector3(MathF.Cos(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Sin(Yaw) * MathF.Cos(Pitch));
                front = Vector3.TransformNormal(front, rotMat);
                Position = -front * (Target - Position).Length + Target;

                isPoseDirty = true;
            }

            if (isRightMousePressed)
            {
                Vector3 front = Vector3.Normalize(Target - Position);
                Vector3 right = Vector3.Normalize(Vector3.Cross(Up, front));
                Position += right * xoffset * TransSensitivity;
                Target += right * xoffset * TransSensitivity;
                Vector3 camUp = Vector3.Normalize(Vector3.Cross(right, front));
                Position += camUp * yoffset * TransSensitivity;
                Target += camUp * yoffset * TransSensitivity;

                isPoseDirty = true;
            }
        }

        internal void ProcessWheel(float dx, float dy)
        {
            Vector3 front = Vector3.Normalize(Target - Position);
            Position += front * dy * ZoomSensitivity;
            Target += front * dy * ZoomSensitivity;
            isPoseDirty = true;
        }

        internal void ProcessRollKey(int d)
        {
            Vector3 front = Vector3.Normalize(Target - Position);
            Vector3 right = Vector3.Cross(front, Up);
            Vector3 newUp = Up + right * (d * RollSensitivity / right.Length);
            Up = Vector3.Normalize(newUp);
            isPoseDirty = true;
        }

        internal void UpdateResolution(int width, int height)
        {
            Width = width;
            Height = height;
            isIntrinDirty = true;
        }
    }
}
