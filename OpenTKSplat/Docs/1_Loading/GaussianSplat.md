# What is a Gaussian Splat?

This is loosely based on my own reading, having implemented this renderer, as well as this [article](https://www.plainconcepts.com/3d-gaussian-splatting/). 

Essentially Gaussian Splats, are an evolution of NeRFs, which themselves are an evolution of simpler Photogrammetry methods. 

All of these techniques start with a set of images of some subject. This set of images are then used to reconstruct the geometry of the subject. Photogrammetry works well at capturing hard surfaces with clear and obvious boundaries but fails on objects that have complex light interactions like reflection and refraction, and objects that are more amorphous like the sky.

NeRFs (Neural Radiance Fields) essentially reframed the problem into a way that could be trained like a machine learning model. So you would use the positions of the images, as well as the images themselves as training data and use gradient decent to optimize the parameters in the NeRF. These parameters would then be ray marched through to synthesize new views.

This works, but is very computationally expensive, and doesn't fit very well on hardware that does not have neural engines. With Gaussian Splats you are essentially using that same reframing as NeRFs, where you are using the positions of the images and the images themselves as the error in gradient decent, but instead of training and evaluating a amorphous blob of weights with a neural engine, you make a set of particles that you can run a backward pass on in a very similar way as NeRFs, using gradient decent. and because you are not training this unstructured blob of weights that are fully abstract, the particles are more "spatial" making them easier to train and they give you more realistic and physical looking reconstructions.

This is all a really complicated way of saying Gaussian Splats are a fancy technique for generating a really high quality particle field that not only mimics the color and geometry of the scene, but can mimic how the colors in the scene change as the camera moves around, allowing for reflections, refractions, and other anisotropic lighting effects.

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

Normally a 3D Gaussian would be represented by a 3D covariant matrix, but the writers of the 3D Gaussian Splatting paper decided to represent it in a 2d form, as an ellipsoid centered on the Position, Rotation and Scale as defined in the GaussianSplat struct above. The 3D covariant matrix is then calculated in the shader for each splat. Due to how constrained bandwidth is on the GPU its faster to just compute the 3D covariant matrix every time instead of retrieving it in the particle.

# Spherical Harmonics?

Spherical Harmonics are used in computer graphics to approximate light variations across a surface. They are mathematical representations that encapsulate how light reflects and refracts from different directions. In the GaussianSplat struct, the SphericalHarmonics array stores coefficients for these functions, enabling the representation of complex lighting effects like anisotropy in a compact form.

[This is a much better, and much more math-y-er explanation then I could possibly provide.](https://patapom.com/blog/SHPortal/)