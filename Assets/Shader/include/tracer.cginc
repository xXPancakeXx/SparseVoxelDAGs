#define MAX_SCALE 23

struct Ray
{
    float3 origin;
    float3 direction;
};

struct DepthData
{
    int offset;
    float maxT;
    int attributeIndex;
};

Ray CreateRay(float3 origin, float3 direction)
{
    Ray ray;
    ray.origin = origin;
    ray.direction = direction;
    return ray;
}

Ray CreateCameraRay(float2 uv, float4x4 camToWorld, float4x4 camInverseProj)
{
    // Transform the camera origin to world space
    float3 origin = mul(camToWorld, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;

    // Invert the perspective projection of the view-space position
    float3 direction = mul(camInverseProj, float4(uv, 0.0f, 1.0f)).xyz;
    // Transform the direction from camera to world space and normalize
    direction = mul(camToWorld, float4(direction, 0.0f)).xyz;
    direction = normalize(direction);
    return CreateRay(origin, direction);
}

float3 GetHitWorldPos(Ray ray, TraceResult res) {
    return ray.origin + res.t * ray.direction;
}