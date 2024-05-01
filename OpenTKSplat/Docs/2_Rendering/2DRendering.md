# Rendering Particles Really Fast

Now that we have this set of particles loading from a file we need some way of displaying them on screen. My renderer is based off of this [python + opengl renderer](https://github.com/limacv/GaussianSplattingViewer). 

Essentially we allocate a single quad made of two triangles, and use standard instanced rendering techniques in opengl to render this quad a few million times, once for each gaussian splat that we loaded.

