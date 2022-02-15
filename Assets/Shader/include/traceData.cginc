
struct TraceResult {
    float t;
    float minT;
    float maxT;
    float3 bT;
    float3 dT;
    float3 voxelPos;
    float3 hitPos;
};

TraceResult CreateTraceResult() {
    TraceResult res;

    res.t = 0;
    res.minT = 0;
    res.maxT = 0;
    res.bT = 0;
    res.dT = 0;
    res.voxelPos = 0;
    res.hitPos = 0;

    return res;
}