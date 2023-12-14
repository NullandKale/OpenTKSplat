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
