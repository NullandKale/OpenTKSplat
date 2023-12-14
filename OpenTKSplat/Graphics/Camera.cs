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

        /*
    class Camera:
        def __init__(self, h, w):
            self.znear = 0.01
            self.zfar = 100
            self.h = h
            self.w = w
            self.fovy = np.pi / 2
            self.position = np.array([0.0, 0.0, 3.0])
            self.target = np.array([0.0, 0.0, 0.0])
            self.up = np.array([0.0, -1.0, 0.0])
            self.yaw = -np.pi / 2
            self.pitch = 0

            self.is_pose_dirty = True
            self.is_intrin_dirty = True

            self.last_x = 640
            self.last_y = 360
            self.first_mouse = True

            self.is_leftmouse_pressed = False
            self.is_rightmouse_pressed = False

            self.rot_sensitivity = 0.02
            self.trans_sensitivity = 0.01
            self.zoom_sensitivity = 0.08
            self.roll_sensitivity = 0.03
 */
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
        /*
        def _global_rot_mat(self):
            x = np.array([1, 0, 0])
            z = np.cross(x, self.up)
            z = z / np.linalg.norm(z)
            x = np.cross(self.up, z)
            return np.stack([x, self.up, z], axis=-1)
 */
        private Matrix4 GlobalRotMat()
        {
            Vector3 x = new Vector3(1f, 0f, 0f);
            Vector3 z = Vector3.Cross(x, Up);
            z.Normalize();
            x = Vector3.Cross(Up, z);
            return new Matrix4(new Vector4(x, 0), new Vector4(Up, 0), new Vector4(z, 0), Vector4.UnitW);
        }
        /*
        def get_view_matrix(self):
            return np.array(glm.lookAt(self.position, self.target, self.up))
 */
        internal Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Target, Up);
        }
        /*
        def get_project_matrix(self):
            project_mat = glm.perspective(
                self.fovy,
                self.w / self.h,
                self.znear,
                self.zfar
            )
            return np.array(project_mat).astype(np.float32)


 */
        internal Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(Fovy, Width / (float)Height, ZNear, ZFar);
        }
        /*
        def get_htanfovxy_focal(self):
            htany = np.tan(self.fovy / 2)
            htanx = htany / self.h * self.w
            focal = self.h / (2 * htany)
            return [htanx, htany, focal]

        def get_focal(self):
            return self.h / (2 * np.tan(self.fovy / 2))
 */
        internal Vector3 GetHtanFovxyFocal()
        {
            float htany = MathF.Tan(Fovy / 2);
            float htanx = htany / Height * Width;
            float focal = Height / (2 * htany);
            return new Vector3(htanx, htany, focal);
        }

        /*
        def process_mouse(self, xpos, ypos):
            if self.first_mouse:
                self.last_x = xpos
                self.last_y = ypos
                self.first_mouse = False

            xoffset = xpos - self.last_x
            yoffset = self.last_y - ypos
            self.last_x = xpos
            self.last_y = ypos

            if self.is_leftmouse_pressed:
                self.yaw += xoffset * self.rot_sensitivity
                self.pitch += yoffset * self.rot_sensitivity

                self.pitch = np.clip(self.pitch, -np.pi / 2, np.pi / 2)

                front = np.array([np.cos(self.yaw) * np.cos(self.pitch), 
                                np.sin(self.pitch), np.sin(self.yaw) * 
                                np.cos(self.pitch)])
                front = self._global_rot_mat() @ front.reshape(3, 1)
                front = front[:, 0]
                self.position[:] = - front * np.linalg.norm(self.position - self.target) + self.target

                self.is_pose_dirty = True

            if self.is_rightmouse_pressed:
                front = self.target - self.position
                front = front / np.linalg.norm(front)
                right = np.cross(self.up, front)
                self.position += right * xoffset * self.trans_sensitivity
                self.target += right * xoffset * self.trans_sensitivity
                cam_up = np.cross(right, front)
                self.position += cam_up * yoffset * self.trans_sensitivity
                self.target += cam_up * yoffset * self.trans_sensitivity

                self.is_pose_dirty = True

 */
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

        /*

        def process_wheel(self, dx, dy):
            front = self.target - self.position
            front = front / np.linalg.norm(front)
            self.position += front * dy * self.zoom_sensitivity
            self.target += front * dy * self.zoom_sensitivity
            self.is_pose_dirty = True


 */
        internal void ProcessWheel(float dx, float dy)
        {
            Vector3 front = Vector3.Normalize(Target - Position);
            Position += front * dy * ZoomSensitivity;
            Target += front * dy * ZoomSensitivity;
            isPoseDirty = true;
        }

        /*
        def process_roll_key(self, d):
            front = self.target - self.position
            right = np.cross(front, self.up)
            new_up = self.up + right * (d * self.roll_sensitivity / np.linalg.norm(right))
            self.up = new_up / np.linalg.norm(new_up)
            self.is_pose_dirty = True
         */
        internal void ProcessRollKey(int d)
        {
            Vector3 front = Vector3.Normalize(Target - Position);
            Vector3 right = Vector3.Cross(front, Up);
            Vector3 newUp = Up + right * (d * RollSensitivity / right.Length);
            Up = Vector3.Normalize(newUp);
            isPoseDirty = true;
        }

        /*
def update_resolution(self, height, width):
    self.h = height
    self.w = width
    self.is_intrin_dirty = True
         */
        internal void UpdateResolution(int width, int height)
        {
            Width = width;
            Height = height;
            isIntrinDirty = true;
        }
    }
}
