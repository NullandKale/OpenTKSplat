#version 430 core

#define SH_C0 0.28209479177387814f
#define SH_C1 0.4886025119029199f

#define SH_C2_0 1.0925484305920792f
#define SH_C2_1 -1.0925484305920792f
#define SH_C2_2 0.31539156525252005f
#define SH_C2_3 -1.0925484305920792f
#define SH_C2_4 0.5462742152960396f

#define SH_C3_0 -0.5900435899266435f
#define SH_C3_1 2.890611442640554f
#define SH_C3_2 -0.4570457994644658f
#define SH_C3_3 0.3731763325901154f
#define SH_C3_4 -0.4570457994644658f
#define SH_C3_5 1.445305721320277f
#define SH_C3_6 -0.5900435899266435f

#define POS_IDX 0
#define ROT_IDX 3
#define SCALE_IDX 7
#define OPACITY_IDX 10
#define SH_IDX 11

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

out vec3 color;
out float alpha;
out vec3 conic;
out vec2 coordxy;  // local coordinate in quad, unit in pixel

mat3 computeCov3D(vec3 scale, vec4 q)
{
    mat3 S = mat3(0.f);
    
	S[0][0] = scale.x;
	S[1][1] = scale.y;
	S[2][2] = scale.z;

	float r = q.x;
	float x = q.y;
	float y = q.z;
	float z = q.w;

    mat3 R = mat3(
		1.f - 2.f * (y * y + z * z), 2.f * (x * y - r * z), 2.f * (x * z + r * y),
		2.f * (x * y + r * z), 1.f - 2.f * (x * x + z * z), 2.f * (y * z - r * x),
		2.f * (x * z - r * y), 2.f * (y * z + r * x), 1.f - 2.f * (x * x + y * y)
	);

    mat3 M = S * R;
    mat3 Sigma = transpose(M) * M;
    return Sigma;
}

vec3 computeCov2D(vec4 mean_view, float focal_x, float focal_y, float tan_fovx, float tan_fovy, mat3 cov3D, mat4 viewmatrix)
{
    vec4 t = mean_view;

    // why need this? Try remove this later
    float limx = 1.3f * tan_fovx;
    float limy = 1.3f * tan_fovy;
    float txtz = t.x / t.z;
    float tytz = t.y / t.z;
    t.x = min(limx, max(-limx, txtz)) * t.z;
    t.y = min(limy, max(-limy, tytz)) * t.z;

    mat3 J = mat3(
        focal_x / t.z, 0.0f, -(focal_x * t.x) / (t.z * t.z),
		0.0f, focal_y / t.z, -(focal_y * t.y) / (t.z * t.z),
		0, 0, 0
    );
    mat3 W = transpose(mat3(viewmatrix));
    mat3 T = W * J;

    mat3 cov = transpose(T) * transpose(cov3D) * T;

    // Apply low-pass filter: every Gaussian should be at least
	// one pixel wide/high. Discard 3rd row and column.
	cov[0][0] += 0.3f;
	cov[1][1] += 0.3f;
    return vec3(cov[0][0], cov[0][1], cov[1][1]);
}

vec3 get_vec3(int offset)
{
	return vec3(g_data[offset], g_data[offset + 1], g_data[offset + 2]);
}

vec4 get_vec4(int offset)
{
	return vec4(g_data[offset], g_data[offset + 1], g_data[offset + 2], g_data[offset + 3]);
}

const float CULL_THRESHOLD = 1.3;

void main()
{
	int boxid = gi[gl_InstanceID];

	const int total_dim = 3 + 4 + 3 + 1 + 48;
	int start = boxid * total_dim;

	vec4 g_pos = vec4(get_vec3(start + POS_IDX), 1.f);

    vec4 g_pos_view = view_matrix * g_pos;
    vec4 g_pos_screen = projection_matrix * g_pos_view;

	g_pos_screen.xyz = g_pos_screen.xyz / g_pos_screen.w;
    g_pos_screen.w = 1.f;

	// Optimized culling check
	if (g_pos_screen.x < -CULL_THRESHOLD || g_pos_screen.x > CULL_THRESHOLD ||
		g_pos_screen.y < -CULL_THRESHOLD || g_pos_screen.y > CULL_THRESHOLD ||
		g_pos_screen.z < -CULL_THRESHOLD || g_pos_screen.z > CULL_THRESHOLD) {
		gl_Position = vec4(0, 0, -1, 0); // Discard in clip space
		return;
	}


	vec4 g_rot = get_vec4(start + ROT_IDX);
	vec3 g_scale = get_vec3(start + SCALE_IDX);

    mat3 cov3d = computeCov3D(g_scale, g_rot);

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
    
    vec2 quadwh_scr = vec2(3.f * sqrt(cov2d.x), 3.f * sqrt(cov2d.z));  // screen space half quad height and width
    vec2 quadwh_ndc = quadwh_scr / wh * 2;  // in ndc space

    g_pos_screen.xy = g_pos_screen.xy + position * quadwh_ndc;
    coordxy = position * quadwh_scr;
    gl_Position = g_pos_screen;
    
    alpha = g_data[start + OPACITY_IDX];

	// Covert SH to color
	vec3 dir = g_pos.xyz - cam_pos;
    dir = normalize(dir);

	float x = dir.x;
	float y = dir.y;
	float z = dir.z;

	float xx = x * x;
	float yy = y * y;
	float zz = z * z;
	float xy = x * y;
	float yz = y * z;
	float xz = x * z;

	// Fetch all SH data at once

	int sh_start = start + SH_IDX;
	vec3 sh_data[16]; // Array to hold all SH data
	for (int i = 0; i < 16; ++i) {
		sh_data[i] = get_vec3(sh_start + i * 3);
	}

	color = SH_C0 * sh_data[0];

	color = color - SH_C1 * y * sh_data[1] + SH_C1 * z * sh_data[2] - SH_C1 * x * sh_data[3];

	color = color +
		SH_C2_0 * xy * sh_data[4] +
		SH_C2_1 * yz * sh_data[5] +
		SH_C2_2 * (2.0f * zz - xx - yy) * sh_data[6] +
		SH_C2_3 * xz * sh_data[7] +
		SH_C2_4 * (xx - yy) * sh_data[8];

	color = color +
		SH_C3_0 * y * (3.0f * xx - yy) * sh_data[9] +
		SH_C3_1 * xy * z * sh_data[10] +
		SH_C3_2 * y * (4.0f * zz - xx - yy) * sh_data[11] +
		SH_C3_3 * z * (2.0f * zz - 3.0f * xx - 3.0f * yy) * sh_data[12] +
		SH_C3_4 * x * (4.0f * zz - xx - yy) * sh_data[13] +
		SH_C3_5 * z * (xx - yy) * sh_data[14] +
		SH_C3_6 * x * (xx - 3.0f * yy) * sh_data[15];

	color += 0.5f;
}
