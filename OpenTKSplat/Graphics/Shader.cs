using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System.Xml.Linq;

namespace OpenTKSplat.Graphics
{
    public class Shader
    {
        public readonly int Handle;

        public Shader(string vertexPath, string fragmentPath, bool load = true)
        {
            string vertexShaderSource;
            string fragmentShaderSource;

            if (load)
            {
                // Read the shader source from files
                vertexShaderSource = File.ReadAllText(vertexPath);
                fragmentShaderSource = File.ReadAllText(fragmentPath);
            }
            else
            {
                // Use the string parameters directly as shader source
                vertexShaderSource = vertexPath;
                fragmentShaderSource = fragmentPath;
            }

            // Compile the vertex shader
            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            CompileShader(vertexShader);

            // Compile the fragment shader
            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            CompileShader(fragmentShader);

            // Link the vertex and fragment shader into a shader program
            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vertexShader);
            GL.AttachShader(Handle, fragmentShader);
            LinkProgram(Handle);

            // Detach and delete the shaders as they're no longer needed
            GL.DetachShader(Handle, vertexShader);
            GL.DetachShader(Handle, fragmentShader);
            GL.DeleteShader(fragmentShader);
            GL.DeleteShader(vertexShader);
        }

        private static void CompileShader(int shader)
        {
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var code);
            if (code != (int)All.True)
            {
                var infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"Error occurred whilst compiling Shader({shader}).\n\n{infoLog}");
            }
        }

        private static void LinkProgram(int program)
        {
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var code);
            if (code != (int)All.True)
            {
                throw new Exception($"Error occurred whilst linking Program({program})");
            }
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }

        public void SetVector3(string name, Vector3 vector)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.Uniform3(location, ref vector);
        }


        public void SetMatrix4(string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.UniformMatrix4(location, false, ref matrix);
        }

        public void Dispose()
        {
            GL.DeleteProgram(Handle);
        }

        internal void SetInt(string name, int val)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.Uniform1(location, val);
        }

        internal void SetFloat(string name, float val)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.Uniform1(location, val);
        }
    }
}
