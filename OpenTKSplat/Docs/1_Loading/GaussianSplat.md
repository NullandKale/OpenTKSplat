# What is a Gaussian Splat?

This is loosely based on my own reading, having implemented this renderer, as well as this [article](https://www.plainconcepts.com/3d-gaussian-splatting/). 

Essentially Gaussian Splats, are an evolution of NeRFs, which themselves are an evolution of simpler Photogrammetry algorithms. 

All of these techniques start with a set of images of some subject. This set of images are then used to reconstruct the geometry of the subject. Photogrammetry works well at capturing hard surfaces with clear and obvious boundaries but fails on objects that have complex light interactions like reflection and refraction, and objects that are more amorphous like the sky.

NeRFs (Neural Radiance Fields) essentially reframed the problem into a way that could be trained like a machine learning model. So you would use the positions of the images, as well as the images themselves as training data and use gradient decent to optimize the parameters in the NeRF. These parameters would be then ray marched through to synthesize new views. 

This works, but was very computationally expensive. With Gaussian Splats you are essentially using that same reframing as NeRFs but instead of this amorphous blob of weights you make a set of particles that you can train in the same way using gradient decent.

This is all a really fancy way of saying Gaussian Splats are a fancy technique for generating a really high quality particle field that not only mimics the color and geometry of the scene, but can mimic how the colors in the scene change as the camera moves around, allowing for reflections, refractions, and other anisotropic lighting effects.

# How does the code in this repo represent a Gaussian Splat?

Gaussian Splats are really just particles with extra data built in and thinking of them in that way will make reasoning about them easier.

Each Gaussian Splat consists of this data:

```csharp
public unsafe struct GaussianSplat
{
    // xyz world space position
    public Vector3 Position;
    // rotation quaternion
    public Vector4 Rotation;
    // xyz scalers
    public Vector3 Scale;
    // opacity
    public float Opacity;
    // direction dependent color
    public fixed float SphericalHarmonics[48];
}
```
[CODE](../../Data/GaussianSplat.cs)

# Spherical Harmonics?

Spherical Harmonics are used in computer graphics to approximate light variations across a surface. They are mathematical representations that encapsulate how light reflects and refracts from different directions. In the GaussianSplat struct, the SphericalHarmonics array stores coefficients for these functions, enabling the representation of complex lighting effects like anisotropy in a compact form.

[This is a much better, and much more math-y-er explanation then I could possibly provide.](https://patapom.com/blog/SHPortal/)