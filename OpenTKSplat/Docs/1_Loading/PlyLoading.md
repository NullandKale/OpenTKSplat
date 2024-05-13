# How do we store Gaussian Splats?

Currently there are many forms for storing splats but this repo just supports the simplest storage method: the .ply file. 

This stores an array of structs, and the definition of that struct, such that it is easy to parse and is not platform dependent.

If you open a .ply file in a text editor like VS code or notepad++ you will see something that looks like this followed by a bunch of garbage.

```ply
ply
format binary_little_endian 1.0
element vertex 329925
property float x
property float y
property float z
property float nxx
property float ny
property float nz
property float f_dc_0
property float f_dc_1
property float f_dc_2
property float f_rest_0
property float f_rest_1
property float f_rest_2
property float f_rest_3
property float f_rest_4
property float f_rest_5
property float f_rest_6
property float f_rest_7
property float f_rest_8
property float f_rest_9
property float f_rest_10
property float f_rest_11
property float f_rest_12
property float f_rest_13
property float f_rest_14
property float f_rest_15
property float f_rest_16
property float f_rest_17
property float f_rest_18
property float f_rest_19
property float f_rest_20
property float f_rest_21
property float f_rest_22
property float f_rest_23
property float f_rest_24
property float f_rest_25
property float f_rest_26
property float f_rest_27
property float f_rest_28
property float f_rest_29
property float f_rest_30
property float f_rest_31
property float f_rest_32
property float f_rest_33
property float f_rest_34
property float f_rest_35
property float f_rest_36
property float f_rest_37
property float f_rest_38
property float f_rest_39
property float f_rest_40
property float f_rest_41
property float f_rest_42
property float f_rest_43
property float f_rest_44
property float opacity
property float scale_0
property float scale_1
property float scale_2
property float rot_0
property float rot_1
property float rot_2
property float rot_3
end_header
```

This is the header and it specifies the count of particles, and the same GaussianSplat struct that we were looking at before, you will see many more parameters, but this is just because the .ply file does not pack the data into arrays or Vec3 / Vec4's for us.

The garbage data after the header is the exact bytes of the set of particles, in the order and with the data type specified in the header.

You could in theory copy this data directly into an object in memory, but it would be in a weird order, and include some extra data we do not need. I wanted the GaussianSplats to be in nice little structs that we could reason about easily, so I needed a way to easily and quickly parse the data inside the .ply file. 

# The neat little trick, treating the ply file like a dict

```csharp
using OpenTK.Mathematics;

namespace OpenTKSplat.Data
{
    // for faster parsing we initially store the Gaussian Data as a struct of arrays
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

        // this flattens the struct of arrays to an array of structs for use on the gpu
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

        // This is the actual entry point for the loading functionality.
        public static GaussianData LoadPly(string path)
        {
            // load the file and create a PlyData object.
            PlyData ply = PlyData.Load(path);
            int vertexCount = ply.vertexCount;
            int maxShDegree = 3;
            int extraFeatureCount = (maxShDegree + 1) * (maxShDegree + 1) - 1;
            int shDimension = 3 * extraFeatureCount + 3; // 3 for diffuse color + rest for spherical harmonics
            
            // pre allocate all the arrays.
            GaussianData data = new GaussianData(vertexCount, shDimension);

            // this should always be true, and we could probably remove it.
            bool rotFieldsExist = ply.HasField("rot_0");

            // I process all of the splats in parallel across as many cores as possible
            // its REALLY inefficient to send millions of tasks to the C# task scheduler
            // instead I run one task per core that loops over a chunk of the splats
            int coreCount = Environment.ProcessorCount;
            int chunkSize = vertexCount / coreCount;

            // we may have up to coreCount - 1 leftovers that we cannot forget about
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


        // this function is called in parallel and queries the data stored in the PlyData
        // some of the data in the ply file needs post processing, this is another reason
        // why doing this on all cores in parallel such a large speed up.
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
            // They have weird names so you need to set them up just right.
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
                int baseIndexCSharp = 3 + j * 3;
                for (int k = 0; k < 3; k++)
                {
                    int indexExtra = j + k * extraFeatureCount;
                    int indexSH = baseIndexCSharp + k;
                    data.SphericalHarmonics[i, indexSH] = featuresExtra[indexExtra];
                }
            }
        }
    }

}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public struct PlyProperty
{
    public string Name;
    public Type DataType;
    public int ByteSize;
}

public class PlyData
{
    public List<PlyProperty> properties;
    private Dictionary<string, (PlyProperty property, int offset)> propertyMap;
    private byte[] dataBuffer;
    private int vertexSize;
    public int vertexCount;

    private PlyData()
    {
        properties = new List<PlyProperty>();
        propertyMap = new Dictionary<string, (PlyProperty, int)>(StringComparer.InvariantCultureIgnoreCase);
    }

    public static PlyData Load(string filePath)
    {
        var plyData = new PlyData();
        using (FileStream fs = new FileStream(filePath, FileMode.Open))
        using (BinaryReader br = new BinaryReader(fs))
        {
            plyData.ReadHeader(br);
            plyData.ReadData(br);
        }
        return plyData;
    }

    private void ReadHeader(BinaryReader br)
    {
        properties.Clear();
        vertexSize = 0;
        vertexCount = 0;

        bool inHeader = true;
        while (inHeader)
        {
            string line = ReadLine(br);
            if (line.StartsWith("element vertex"))
            {
                vertexCount = int.Parse(line.Split(' ')[2]);
            }
            else if (line.StartsWith("property"))
            {
                AddProperty(line);
            }
            else if (line == "end_header")
            {
                inHeader = false;
            }
        }
    }

    private void ReadData(BinaryReader br)
    {
        int totalDataSize = vertexSize * vertexCount;
        dataBuffer = new byte[totalDataSize];
        br.Read(dataBuffer, 0, totalDataSize);

        // Verify the number of vertices read matches the header declaration
        if (br.BaseStream.Position != br.BaseStream.Length)
        {
            throw new InvalidDataException("The number of vertices does not match the header declaration.");
        }
    }

    private void AddProperty(string line)
    {
        string[] parts = line.Split(' ');
        PlyProperty property = new PlyProperty
        {
            Name = parts[2],
            DataType = GetType(parts[1]),
            ByteSize = GetByteSize(parts[1])
        };
        int offset = properties.Sum(p => p.ByteSize);
        properties.Add(property);
        propertyMap[property.Name] = (property, offset);
        vertexSize += property.ByteSize;

    }

    private int GetByteSize(string type)
    {
        switch (type)
        {
            case "uchar": return sizeof(byte);
            case "ushort": return sizeof(ushort);
            case "short": return sizeof(short);
            case "float": return sizeof(float);
            case "uint": return sizeof(uint);
            case "int": return sizeof(int);
            case "double": return sizeof(double);
            default: throw new NotSupportedException($"Property type '{type}' not supported");
        }
    }

    private Type GetType(string type)
    {
        switch (type)
        {
            case "uchar": return typeof(byte);
            case "ushort": return typeof(ushort);
            case "short": return typeof(short);
            case "float": return typeof(float);
            case "uint": return typeof(uint);
            case "int": return typeof(int);
            case "double": return typeof(double);
            default: throw new NotSupportedException($"Property type '{type}' not supported");
        }
    }

    public T GetProperty<T>(int index, string propertyName)
    {
        T val = default;

        if (propertyMap.ContainsKey(propertyName))
        {
            int dataOffset = index * vertexSize + propertyMap[propertyName].offset;

            if (dataOffset >= 0 && dataOffset < dataBuffer.Length)
            {
                val = (T)Convert.ChangeType(BitConverter.ToSingle(dataBuffer, dataOffset), typeof(T));
            }
        }

        return val;
    }

    private string ReadLine(BinaryReader br)
    {
        string line = "";
        char c;
        while ((c = br.ReadChar()) != '\n')
        {
            if (c != '\r') line += c;
        }
        return line;
    }

    public bool HasField(string v)
    {
        return properties.Any(p => p.Name == v);
    }
}

```


## Efficient .ply Data Handling for Gaussian Splats

The .ply file format stores a huge block of data. Loading it naively by processing each particle one at a time can be impractically slow, especially when dealing with millions of particles. To address this, the first step is to parse the file's header and set up a mapping for each property of a particle. This mapping keeps track of the offset and data type for each property, allowing us to access any particle property directly without reading the entire file sequentially.

```csharp
    private Dictionary<string, (PlyProperty property, int offset)> propertyMap;

    private void AddProperty(string line)
    {
        string[] parts = line.Split(' ');
        PlyProperty property = new PlyProperty
        {
            Name = parts[2],
            DataType = GetType(parts[1]),
            ByteSize = GetByteSize(parts[1])
        };
        int offset = properties.Sum(p => p.ByteSize);
        properties.Add(property);
        propertyMap[property.Name] = (property, offset);
        vertexSize += property.ByteSize;
    }
```
[CODE](../../Data/PlyLoader.cs)

Once the properties are mapped, we can utilize the full power of the system's CPUs to read and process the particles in parallel. This parallel processing significantly reduces the time it takes to load all the particles since we're effectively jumping directly to the necessary bytes in the data array.

```csharp
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
                int baseIndexCSharp = 3 + j * 3;
                for (int k = 0; k < 3; k++)
                {
                    int indexExtra = j + k * extraFeatureCount;
                    int indexSH = baseIndexCSharp + k;
                    data.SphericalHarmonics[i, indexSH] = featuresExtra[indexExtra];
                }
            }
        }
```
[CODE](../../Data/GaussianData.cs)

This method ensures that the loading of Gaussian Splats is not only fast but also scales effectively with the size of the dataset and the computational resources available.