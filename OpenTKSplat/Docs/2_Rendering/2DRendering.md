# Rendering Splats. Really Fast.

Now that we have this set of splats loading from a file we need some way of displaying them on screen. My renderer is based off of this [python + opengl renderer](https://github.com/limacv/GaussianSplattingViewer). 

The implementation of a renderer provided by the 3D Gaussian paper used a differentiable compute based tiled rasterizer to do both the rendering (the forward pass) and the gradient decent used during training (the backward pass). To achieve differentiability the renderer in the 3D Gaussian paper tracked some extra per pixel info during the rasterization, that is only needed for the backward pass. In the case of this repo, we do not care about training at all, and as such we can avoid the need to track that extra information, and can avoid using a fully compute based pipeline.

We still need to do some compute though as the splats need to be sorted every time the view matrix or projection matrix changes. The [python + opengl renderer](https://github.com/limacv/GaussianSplattingViewer) that I based my renderer off of does the sorting on the CPU and amortizes the cost across multiple frames. This is slow for many reasons, but the main one is simply the cost of uploading a new set of indices to the GPU every frame.

To avoid this I use a fast radix sort algorithm built into ILGPU. [ILGPU](https://github.com/m4rs-mt/ILGPU) is a gpu compute framework for C# which can use CUDA, OpenCL, and even the CPU to enable cross platform accelerated C#. I however can use non of the fancy cross platform stuff, because I am using a CUDA OpenGL extension that allows us to use CUDA allocated memory in OpenGL. This allows us to avoid all large per frame copy steps.

# Rendering Method

[All of the code for this section will be found here](../../Graphics/Window.cs)

## Setup and Initialization:

First we need to set the blending mode for the splats. To maintain the differentiability splats do not use any fancy order independent transparency methods, and just use standard alpha blending. This is why we must sort every frame, and why we need to set the following blending mode:

```csharp
GL.Disable(EnableCap.CullFace);
GL.Enable(EnableCap.Blend);
GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
```

Now we need the quad that we will draw for each splat, pretty standard stuff.
```csharp
private void SetupGeometry()
{
    // Quad vertices and faces as per Python code
    float[] quadVertices = new float[] { -1, 1, 1, 1, 1, -1, -1, -1 };
    uint[] quadFaces = new uint[] { 0, 1, 2, 0, 2, 3 };

    vao = GL.GenVertexArray();
    GL.BindVertexArray(vao);

    vertexBuffer = GL.GenBuffer();
    GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
    GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

    elementBuffer = GL.GenBuffer();
    GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBuffer);
    GL.BufferData(BufferTarget.ElementArrayBuffer, quadFaces.Length * sizeof(uint), quadFaces, BufferUsageHint.StaticDraw);

    // Get the location of the "position" attribute from the shader
    int positionLocation = GL.GetAttribLocation(shader.Handle, "position");

    // Set the vertex attribute pointers
    GL.VertexAttribPointer(positionLocation, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
    GL.EnableVertexAttribArray(positionLocation);

    // Unbind the VAO
    GL.BindVertexArray(0);
}
```

We now need to then upload the Gaussian Splat data to the gpu as an SSBO, and setup the vertex attributes. 

```csharp
private void SetupGaussianData()
{
    int gaussianSize = Unsafe.SizeOf<GaussianSplat>();
    int totalSize = gaussians.Length * gaussianSize;

    // Generate a buffer for the gaussian data
    int gaussianBuffer = GL.GenBuffer();
    GL.BindBuffer(BufferTarget.ShaderStorageBuffer, gaussianBuffer);
    GL.BufferData(BufferTarget.ShaderStorageBuffer, totalSize, gaussians, BufferUsageHint.StaticDraw);

    // Bind this buffer to a binding point, e.g., 0
    GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, gaussianBuffer);

    // Set up vertex attributes for Gaussian data
    // Position
    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, gaussianSize, 0);
    GL.EnableVertexAttribArray(0);

    // Rotation
    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, gaussianSize, 3 * sizeof(float));
    GL.EnableVertexAttribArray(1);

    // Scale
    GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, gaussianSize, 7 * sizeof(float));
    GL.EnableVertexAttribArray(2);

    // Opacity
    GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, gaussianSize, 10 * sizeof(float));
    GL.EnableVertexAttribArray(3);

    // Spherical Harmonics
    for (int i = 0; i < 48; ++i)
    {
        GL.VertexAttribPointer(4 + i, 1, VertexAttribPointerType.Float, false, gaussianSize, 11 * sizeof(float) + i * sizeof(float));
        GL.EnableVertexAttribArray(4 + i);
    }
}
```

We also need to setup the index buffer but that is handled by the ILGPU OpenGL exchange buffer we use with CUDA, you can read about it [here](../3_Extras/ILGPUOpenGLExchangeBuffer.md). For the purposes of this doc you can just assume its like a normal index buffer.

## Rendering a frame.

Now that we have all the required setup done lets walk through how the rendering happens.

We are going to ignore how the sorting works for now, you can read more about it [here](../3_Extras/Sorting.md)

```csharp
// Get the camera matrices 
viewMatrix = camera.GetViewMatrix();
projectionMatrix = camera.GetProjectionMatrix();

// bind the window framebuffer
// we do this every frame to support the hologram rendering
GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

// clear framebuffer
GL.Clear(ClearBufferMask.ColorBufferBit);

// we also set the viewport every frame to support the hologram rendering
GL.Viewport(0, 0, Size.X, Size.Y);

// ###################### void render() #############################
// in the actual code this is abstracted out into a function but I removed
// the indirection to make the code easier to follow

// set shader uniforms
shader.SetMatrix4("view_matrix", viewMatrix);
shader.SetVector3("cam_pos", camera.Position);
shader.SetMatrix4("projection_matrix", projectionMatrix);

// sort indices array using fast gpu accelerated radix sort using CUDA
// this will get its own doc.
sorter.sort(viewMatrix);

// update data in OpenGL indices buffer
GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, sorter.cudaGlInteropIndexBuffer.glBufferHandle);

// draw each splat using standard instanced rendering
GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0, gaussians.Length);

// ###################### end void render() #############################

SwapBuffers();
```

This is all thats needed to render the splats on the CPU side.

# How do the shaders work??

Most of the work is done per vertex in the vert shader and then interpolated across the pixels in the frag shader. This means the per pixel work is relatively small, which is HUGELY important as we are using an alpha blending method of transparency which will cause significant overdraw, as each splat gets drawn back to front.

I will only be providing a high level commentary of how the shader works, as 90% of it is just math-y implementation details, and I am not confident enough in the math to explain how it works perfectly.

## The vert shader

Like a normal vertex shader this shader is responsible for computing the position of the splat and for computing anything that can just be interpolated across the triangles in the fragment shader such as color and alpha.

```glsl

// input data:
layout(location = 0) in vec2 position;

layout(std430, binding = 0) buffer gaussian_data {
	float g_data[];
};

layout (std430, binding=1) buffer gaussian_order {
	int gi[];
};

uniform mat4 view_matrix;
uniform mat4 projection_matrix;
uniform vec3 hfovxy_focal;
uniform vec3 cam_pos;

// vert shader output:
out vec3 color;
out float alpha;
out vec3 conic;
out vec2 coordxy;

void main()
{
    // get index from sorted index buffer
	int boxid = gi[gl_InstanceID];

	const int total_dim = 3 + 4 + 3 + 1 + 48;

    // compute index into SSBO
	int start = boxid * total_dim;

    // get position
	vec4 g_pos = vec4(get_vec3(start + POS_IDX), 1.f);

    // transform world space pos into screen space and view space pos
    vec4 g_pos_view = view_matrix * g_pos;
    vec4 g_pos_screen = projection_matrix * g_pos_view;

	g_pos_screen.xyz = g_pos_screen.xyz / g_pos_screen.w;
    g_pos_screen.w = 1.f;

	// Quickly Cull anything we know is off screen
	if (g_pos_screen.x < -CULL_THRESHOLD || g_pos_screen.x > CULL_THRESHOLD ||
		g_pos_screen.y < -CULL_THRESHOLD || g_pos_screen.y > CULL_THRESHOLD ||
		g_pos_screen.z < -CULL_THRESHOLD || g_pos_screen.z > CULL_THRESHOLD) {
		gl_Position = vec4(0, 0, -1, 0); // Discard in clip space
		return;
	}

    // get rot and scale
	vec4 g_rot = get_vec4(start + ROT_IDX);
	vec3 g_scale = get_vec3(start + SCALE_IDX);

    // as defined in the 3D Gaussian Splatting paper, reconstruct the 3D covariant matrix for the gaussian based on the rot and scale.
    mat3 cov3d = computeCov3D(g_scale, g_rot);

    // compute 2D covariant of the gaussian  
    vec2 wh = 2 * hfovxy_focal.xy * hfovxy_focal.z;
    vec3 cov2d = computeCov2D(g_pos_view, 
                              hfovxy_focal.z, 
                              hfovxy_focal.z, 
                              hfovxy_focal.x, 
                              hfovxy_focal.y, 
                              cov3d, 
                              view_matrix);

    // Invert covariance (EWA algorithm)
	float det = (cov2d.x * cov2d.z - cov2d.y * cov2d.y);

	if (det == 0.0f)
		gl_Position = vec4(0.f, 0.f, 0.f, 0.f);
    
    float det_inv = 1.f / det;
	conic = vec3(cov2d.z * det_inv, -cov2d.y * det_inv, cov2d.x * det_inv);
    
    // use the 2d covariant to calculate the screen space 
    vec2 quadwh_scr = vec2(3.f * sqrt(cov2d.x), 3.f * sqrt(cov2d.z));  // screen space half quad height and width
    vec2 quadwh_ndc = quadwh_scr / wh * 2;  // in ndc space

    g_pos_screen.xy = g_pos_screen.xy + position * quadwh_ndc;
    coordxy = position * quadwh_scr;
    gl_Position = g_pos_screen;
    
    alpha = g_data[start + OPACITY_IDX];
    
    // sh color calculation would go here, but I omitted it for brevity
}
```
[CODE](../../Assets/Shaders/gau_vert.glsl)

The vert shader transforms the splats, culls them, and computes the anisotropic color using the sh data, now we just need to draw that color with the required alpha value and the alpha for the 3D Gaussian. This is done in the frag shader.

```glsl
#version 430 core

in vec3 color;
in float alpha;
in vec3 conic;
in vec2 coordxy;

out vec4 FragColor;

void main()
{
    // using the conic and position of this pixel calculate the gaussian gradient
    float power = -0.5f * (conic.x * coordxy.x * coordxy.x + conic.z * coordxy.y * coordxy.y) - conic.y * coordxy.x * coordxy.y;

    // cull negative powers.
    if (power > 0.f)
        discard;

    // cull alpha values below the minimum
    const float minPower = log(1.f / 255.f / alpha);
    if (power < minPower) {
        discard; // The opacity will definitely be less than the minimum opacity.
    }

    // calculate the opacity of this pixel on this splat
    float opacity = min(0.99f, alpha * exp(power));

    // set the color!
    FragColor = vec4(color, opacity);
}
```
[CODE](../../Assets/Shaders/gau_frag.glsl)