# What is a Gaussian Splat:

Each Gaussian Splat consists of this data:

```csharp
public unsafe struct GaussianSplat
{
    public Vector3 Position;
    public Vector4 Rotation;
    public Vector3 Scale;
    public float Opacity;
    public fixed float SphericalHarmonics[48];
}
```

Most of the data is self explainitory EXCEPT the 