using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BridgeInProc
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Window
    {
        private readonly UInt32 handle;

        public Window(UInt32 handle)
        {
            this.handle = handle;
        }

        public static implicit operator UInt32(Window window)
        {
            return window.handle;
        }

        public static implicit operator Window(UInt32 handle)
        {
            return new Window(handle);
        }
    }

    public enum UniqueHeadIndices : uint
    {
        FirstLookingGlassDevice = uint.MaxValue
    }

    public enum PixelFormats : uint
    {
        NoFormat   = 0x0,
        RGB        = 0x1907,
        RGBA       = 0x1908,
        BGRA       = 0x80E1,
        Red        = 0x1903,
        RGB_DXT1   = 0x83F0,
        RGBA_DXT5  = 0x83F3,
        YCoCg_DXT5 = 0x01,
        A_RGTC1    = 0x8DBB,
        SRGB       = 0x8C41,
        SRGB_A     = 0x8C43,
        R32F       = 0x822E,
        RGBA32F    = 0x8814
    }

    public class Controller
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetDllDirectory(string lpPathName);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "initialize_bridge", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool InitializeInternal();

        public static bool Initialize()
        {
            FileVersionInfo bridge_version = FileVersionInfo.GetVersionInfo(@"bridge_inproc.dll");

            string version = bridge_version.FileVersion;

            if (version.EndsWith(".0"))
            {
                version = version.Substring(0, version.Length - 2);
            }

            string install_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), $"Looking Glass\\Looking Glass Bridge {version}");

            if (Directory.Exists(install_path))
            {
                SetDllDirectory(install_path);
            }

            return InitializeInternal();
        }

        [DllImport(@"bridge_inproc.dll", EntryPoint = "uninitialize_bridge")]
        public static extern bool Uninitialize();

        [DllImport(@"bridge_inproc.dll", EntryPoint = "instance_window_gl")]
        public static extern bool InstanceWindowGL(ref Window windowHandle, bool useClientRenderThread = true, uint headIndex = (uint)UniqueHeadIndices.FirstLookingGlassDevice);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "window_dimensions")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WindowDimensions(Window wnd, ref uint width, ref uint height);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "lock_rendering")]
        public static extern bool LockRendering(Window wnd);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "unlock_rendering")]
        public static extern bool UnlockRendering(Window wnd);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "max_texture_size")]
        public static extern bool MaxTextureSize(Window wnd, ref uint width);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "set_interop_quilt_texture_gl")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetInteropQuiltTextureGL(Window wnd, ulong texture, PixelFormats format, uint width, uint height, uint vx, uint vy, float aspect, float zoom);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "draw_interop_quilt_texture_gl")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DrawInteropQuiltTextureGL(Window wnd, ulong texture, PixelFormats format, uint width, uint height, uint vx, uint vy, float aspect, float zoom);

        [DllImport(@"bridge_inproc.dll", CharSet = CharSet.Unicode, EntryPoint = "save_texture_to_file_gl")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SaveTextureToFileGL(Window wnd, string filename, ulong texture, PixelFormats format, uint width, uint height);

        [DllImport(@"bridge_inproc.dll", CharSet = CharSet.Unicode, EntryPoint = "save_image_to_file")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SaveImageToFile(Window wnd, string filename, IntPtr image, PixelFormats format, ulong width, ulong height);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "instance_window_dx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InstanceWindowDX(IntPtr dxDevice, ref Window windowHandle, bool useClientRenderThread = true, uint headIndex = (uint)UniqueHeadIndices.FirstLookingGlassDevice);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "register_texture_dx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterTextureDX(Window wnd, IntPtr texture);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "unregister_texture_dx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterTextureDX(Window wnd, IntPtr texture);

        [DllImport(@"bridge_inproc.dll", CharSet = CharSet.Unicode, EntryPoint = "save_texture_to_file_dx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SaveTextureToFileDX(Window wnd, string filename, IntPtr texture);

        [DllImport(@"bridge_inproc.dll", EntryPoint = "draw_interop_quilt_texture_dx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DrawInteropQuiltTextureDX(Window wnd, IntPtr texture, uint vx, uint vy, float aspect, float zoom);
    }
}

