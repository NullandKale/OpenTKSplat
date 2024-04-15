using OpenTK.Mathematics;

namespace OpenTKSplat.Data
{
    public unsafe struct VertexData
    {
        public Vector3 Position;
        public Vector4 Rotation;
        public Vector3 Scale;
        public float Opacity;
        public fixed float SphericalHarmonics[48];

        public VertexData(Vector3 position, Vector4 rotation, Vector3 scale, float opacity, float[] sphericalHarmonics)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Opacity = opacity;

            for (int i = 0; i < sphericalHarmonics.Length; i++)
            {
                SphericalHarmonics[i] = sphericalHarmonics[i];
            }
        }
    }
}
