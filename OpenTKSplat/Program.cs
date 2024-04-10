using System;
using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTKSplat.Data;

namespace OpenTKSplat
{
    public class Program
    {
        static void Main(string[] args)
        {
            var nativeWindowSettings = new NativeWindowSettings()
            {
                Size = new Vector2i(800, 600),
                Title = "OpenTK Gaussian Splats",
                WindowState = OpenTK.Windowing.Common.WindowState.Normal,
            };

            using (var window = new Window(GameWindowSettings.Default, nativeWindowSettings))
            {
                window.Run();
            }
        }
    }
}