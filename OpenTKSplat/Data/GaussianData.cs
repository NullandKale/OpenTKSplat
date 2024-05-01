using OpenTK.Mathematics;

namespace OpenTKSplat.Data
{
    public class GaussianData
    {
        public Vector3[] Positions;
        public Vector4[] Rotations;
        public Vector3[] Scales;
        public float[] Opacities;
        public float[,] SphericalHarmonics;

        private GaussianData(int vertexCount, int shDimension)
        {
            Positions = new Vector3[vertexCount];
            Rotations = new Vector4[vertexCount];
            Scales = new Vector3[vertexCount];
            Opacities = new float[vertexCount];
            SphericalHarmonics = new float[vertexCount, shDimension];
        }

        public GaussianSplat[] Flatten()
        {
            int vertexCount = Positions.Length;
            int shDimension = SphericalHarmonics.GetLength(1);

            GaussianSplat[] flatData = new GaussianSplat[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                float[] shData = new float[shDimension];
                for (int j = 0; j < shDimension; j++)
                {
                    shData[j] = SphericalHarmonics[i, j];
                }

                flatData[i] = new GaussianSplat(Positions[i], Rotations[i], Scales[i], Opacities[i], shData);
            }

            return flatData;
        }

        public static GaussianData LoadPly(string path)
        {
            PlyData ply = PlyData.Load(path);
            int vertexCount = ply.vertexCount;
            int maxShDegree = 3;
            int extraFeatureCount = (maxShDegree + 1) * (maxShDegree + 1) - 1;
            int shDimension = 3 * extraFeatureCount + 3; // 3 for diffuse color + rest for spherical harmonics
            
            GaussianData data = new GaussianData(vertexCount, shDimension);

            bool rotFieldsExist = ply.HasField("rot_0");

            int coreCount = Environment.ProcessorCount;
            int chunkSize = vertexCount / coreCount;
            int leftovers = vertexCount % coreCount;

            // Process the chunks
            Parallel.For(0, coreCount, core =>
            {
                int start = core * chunkSize;
                int end = (core + 1) * chunkSize;

                for (int i = start; i < end; i++)
                {
                    LoadVert(i, extraFeatureCount, ply, data, rotFieldsExist);
                }
            });

            // Process any leftovers
            Parallel.For(vertexCount - leftovers, vertexCount, i =>
            {
                LoadVert(i, extraFeatureCount, ply, data, rotFieldsExist);
            });

            return data;
        }

        private static void LoadVert(int i, int extraFeatureCount, PlyData ply, GaussianData data, bool rotFieldsExist)
        {
            // Reading positions
            data.Positions[i] = new Vector3(
                ply.GetProperty<float>(i, "x"),
                ply.GetProperty<float>(i, "y"),
                ply.GetProperty<float>(i, "z")
            );

            // Reading opacities and applying sigmoid function
            data.Opacities[i] = 1f / (1f + MathF.Exp(-ply.GetProperty<float>(i, "opacity")));

            // Reading scales and applying exponential function
            data.Scales[i] = new Vector3(
                MathF.Exp(ply.GetProperty<float>(i, "scale_0")),
                MathF.Exp(ply.GetProperty<float>(i, "scale_1")),
                MathF.Exp(ply.GetProperty<float>(i, "scale_2"))
            );

            // Reading rotations, normalizing if fields exist
            if (rotFieldsExist)
            {
                Vector4 rotation = new Vector4(
                    ply.GetProperty<float>(i, "rot_0"),
                    ply.GetProperty<float>(i, "rot_1"),
                    ply.GetProperty<float>(i, "rot_2"),
                    ply.GetProperty<float>(i, "rot_3")
                );
                data.Rotations[i] = Vector4.Normalize(rotation);
            }
            else
            {
                data.Rotations[i] = new Vector4(1, 0, 0, 0); // Default rotation
            }

            // Handling spherical harmonics and diffuse color features
            float[] featuresDc = new float[3];
            featuresDc[0] = ply.GetProperty<float>(i, "f_dc_0");
            featuresDc[1] = ply.GetProperty<float>(i, "f_dc_1");
            featuresDc[2] = ply.GetProperty<float>(i, "f_dc_2");

            // Handling extra spherical harmonics features
            float[] featuresExtra = new float[extraFeatureCount * 3];
            for (int j = 0; j < extraFeatureCount * 3; j++)
            {
                string property = $"f_rest_{j}";
                featuresExtra[j] = ply.GetProperty<float>(i, property);
            }

            // Combine featuresDc and featuresExtra for final Spherical Harmonics
            for (int j = 0; j < 3; j++)
            {
                data.SphericalHarmonics[i, j] = featuresDc[j];
            }

            for (int j = 0; j < extraFeatureCount; j++)
            {
                int shFeatureStartIndex = 3 + j * 3;
                for (int k = 0; k < 3; k++)
                {
                    int indexExtra = j + k * extraFeatureCount;
                    int indexSH = shFeatureStartIndex + k;
                    data.SphericalHarmonics[i, indexSH] = featuresExtra[indexExtra];
                }
            }
        }
    }

}