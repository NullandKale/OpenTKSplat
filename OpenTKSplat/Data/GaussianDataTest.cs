using OpenTK.Mathematics;
using System.Diagnostics;

namespace OpenTKSplat.Data
{
    public static class Utils
    {
        public static float[] CreateCubeVertices()
        {
            float scale = 1.0f;

            // Define the vertices of the cube
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-scale, -scale, -scale),  // 0
                new Vector3( scale, -scale, -scale),  // 1
                new Vector3( scale,  scale, -scale),  // 2
                new Vector3(-scale,  scale, -scale),  // 3
                new Vector3(-scale, -scale,  scale),  // 4
                new Vector3( scale, -scale,  scale),  // 5
                new Vector3( scale,  scale,  scale),  // 6
                new Vector3(-scale,  scale,  scale)   // 7
            };

            // UV coordinates for each vertex
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0.0f, 0.0f),  // Bottom-left
                new Vector2(1.0f, 0.0f),  // Bottom-right
                new Vector2(1.0f, 1.0f),  // Top-right
                new Vector2(0.0f, 1.0f)   // Top-left
            };

            // Define the triangles of the cube
            int[][] indices = new int[][]
            {
                // Front face
                new int[] { 0, 1, 2, 2, 3, 0 },
                // Back face
                new int[] { 5, 4, 7, 7, 6, 5 },
                // Left face
                new int[] { 3, 7, 4, 4, 0, 3 },
                // Right face
                new int[] { 1, 5, 6, 6, 2, 1 },
                // Top face
                new int[] { 3, 2, 6, 6, 7, 3 },
                // Bottom face
                new int[] { 4, 5, 1, 1, 0, 4 }
            };

            // Compose the final vertex data
            List<float> vertexData = new List<float>();
            foreach (int[] face in indices)
            {
                foreach (int index in face)
                {
                    vertexData.AddRange(new float[] { vertices[index].X, vertices[index].Y, vertices[index].Z });
                    vertexData.AddRange(new float[] { uvs[index % 4].X, uvs[index % 4].Y });
                }
            }

            return vertexData.ToArray();
        }


    }


    public static class GaussianDataTest
    {
        public static void MatchSphericalHarmonics(float[] csharpArray, float[] pythonArray)
        {
            if (csharpArray.Length != pythonArray.Length)
            {
                Console.WriteLine("Arrays have different lengths and cannot be matched.");
                return;
            }

            var matches = new Dictionary<int, int>(); // Key: C# index, Value: Python index

            for (int i = 0; i < csharpArray.Length; i++)
            {
                for (int j = 0; j < pythonArray.Length; j++)
                {
                    if (Math.Abs(csharpArray[i] - pythonArray[j]) < 1e-6) // Adjust tolerance as needed
                    {
                        matches.Add(i, j);
                        break;
                    }
                }
            }

            foreach (var match in matches)
            {
                Console.WriteLine($"C# index {match.Key} matches with Python index {match.Value}");
            }
        }

        public static float[] CorrectSphericalHarmonics(float[] csharpArray)
        {
            float[] correctedArray = new float[csharpArray.Length];

            // The first few indices match directly
            for (int i = 0; i <= 3; i++)
            {
                correctedArray[i] = csharpArray[i];
            }

            // Apply the observed transposition pattern
            for (int i = 4; i < csharpArray.Length; i++)
            {
                int pythonIndex = i + (i - 3) * 2;
                if (pythonIndex < csharpArray.Length)
                {
                    correctedArray[pythonIndex] = csharpArray[i];
                }
            }

            return correctedArray;
        }


        private unsafe static void PrintVertexData(string title, VertexData pythonVertexData, VertexData vertexData)
        {
            Console.WriteLine(title);
            CompareAndPrint("Position", vertexData.Position, pythonVertexData.Position);
            CompareAndPrint("Scale", vertexData.Scale, pythonVertexData.Scale);
            CompareAndPrint("Opacity", vertexData.Opacity, pythonVertexData.Opacity);
            CompareAndPrint("Rotation", vertexData.Rotation, pythonVertexData.Rotation);

            Console.WriteLine("Spherical Harmonics:");
            float[] python = new float[48];
            float[] csharp = new float[48];

            for (int i = 0; i < 48; i++)
            {
                python[i] = pythonVertexData.SphericalHarmonics[i];
                csharp[i] = vertexData.SphericalHarmonics[i];

                if (vertexData.SphericalHarmonics[i] != pythonVertexData.SphericalHarmonics[i])
                {
                    Console.WriteLine($"[{i}]: C#: {vertexData.SphericalHarmonics[i]:e}, Python: {pythonVertexData.SphericalHarmonics[i]:e}");
                }
                else
                {
                    Console.WriteLine($"[{i}]: {vertexData.SphericalHarmonics[i]:e}");
                }
            }
            Console.WriteLine();

            MatchSphericalHarmonics(csharp, python);
        }

        private static void CompareAndPrint<T>(string propertyName, T csharpValue, T pythonValue) where T : IEquatable<T>
        {
            if (!csharpValue.Equals(pythonValue))
            {
                Console.WriteLine($"{propertyName}: C#: {csharpValue}, Python: {pythonValue}");
            }
            else
            {
                Console.WriteLine($"{propertyName}: {csharpValue}");
            }
        }

        public unsafe static void Time()
        {
            string file = "C:\\Users\\zinsl\\Downloads\\output(1)\\output\\point_cloud\\iteration_30000\\point_cloud.ply";

            // Create a Stopwatch instance
            Stopwatch stopwatch = new Stopwatch();

            // Start timing the Ply file loading
            stopwatch.Start();

            // Load the ply file
            GaussianData gaussianData = GaussianData.LoadPly(file);

            // Stop timing the Ply file loading
            stopwatch.Stop();
            Console.WriteLine($"Time taken to load ply file: {stopwatch.ElapsedMilliseconds} ms");

            // Reset and start the Stopwatch before the next operation
            stopwatch.Reset();
            stopwatch.Start();

            // Flatten data
            var data = gaussianData.Flatten();

            // Stop timing the Flatten operation
            stopwatch.Stop();
            Console.WriteLine($"Time taken to flatten data: {stopwatch.ElapsedMilliseconds} ms");
        }

        public unsafe static void Test()
        {
            Time();

            string file = "C:\\Users\\zinsl\\Downloads\\output(1)\\output\\point_cloud\\iteration_30000\\point_cloud.ply";

            // Load the ply file
            GaussianData gaussianData = GaussianData.LoadPly(file);

            // Flatten data
            var data = gaussianData.Flatten();

            // Retrieve specific vertex data
            int vertexIndex = 100;
            VertexData vertexData = data[vertexIndex];

            VertexData pythonVertexData = new VertexData(
                new Vector3(0.7187582f, 1.712764f, 0.9231456f),
                new Vector4(1f, 0f, 0f, 0f),
                new Vector3(0.01418768f, 0.00030839f, 0.01934555f),
                0.9932801f,
                new float[] {
                    -0.588395774f, -1.00623655f, -1.43866456f, 0.0111314151f,
                    0.0131295426f, 0.00113296381f, -0.0302010924f, -0.0400331691f,
                    -0.0751623735f, -0.00340310717f, 0.00612699008f, 0.0412530527f,
                    0.00661085080f, 0.00620262977f, 0.000197900925f, 0.0138699533f,
                    0.0176680256f, 0.0283981394f, -0.0331746116f, -0.0381838381f,
                    -0.0897458494f, -0.00141286582f, 0.0108953761f, 0.0216430761f,
                    0.0571429878f, 0.0447306037f, 0.0298448522f, -0.0224584583f,
                    -0.0156136323f, -0.013048836f, 0.0121729635f, 0.00878350995f,
                    0.00747572025f, 0.0415592231f, 0.0552152023f, 0.0984084234f,
                    -0.00483171036f, -0.00779608684f, -0.0317136869f, -0.0885251835f,
                    -0.0851516575f, -0.0967054665f, -0.00728623988f, -0.0177136455f,
                    0.0219330322f, 0.0145798903f, 0.0223467611f, 0.0436687917f
                }
            );

            // Print the Python vertex data
            PrintVertexData("Vertex Data", pythonVertexData, vertexData);
        }
    }
}