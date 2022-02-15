#include "include/traceData.cginc"
#include "include/tracer.cginc"



inline int FetchChildIndex(StructuredBuffer<uint> data, const uint nodeIdx, const int node, const int childBit)
{
    int lowerIndexLeafOctantMask = childBit - 1;

    //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)
    int childIndexOffset = countbits((uint) (node & lowerIndexLeafOctantMask)) + 1;
    int childRelativePtr = data[nodeIdx + childIndexOffset];
    int childIndex = nodeIdx + childIndexOffset + childRelativePtr;

    return childIndex;
}

void TraceDag(StructuredBuffer<uint> _octreeNodes, int startIndex, int maxDepth, Ray ray, float projFactor, out TraceResult res)
{
    DepthData rayStack[MAX_SCALE];

    //clamp ray direction to minimal value (reduces floating point inacurracy)
    if (abs(ray.direction.x) < 1e-4f) ray.direction.x = 1e-4f;
    if (abs(ray.direction.y) < 1e-4f) ray.direction.y = 1e-4f;
    if (abs(ray.direction.z) < 1e-4f) ray.direction.z = 1e-4f;

    //Forumla for calculating the distance to a plane 
    //t = (lines - ray.origin) / ray.direction
    //t = (lines - ray.origin) * dT
    //t = lines * dT - ray.origin * dT              -> bT = ray.orgin * dT
    //t = lines * dT - bT

    // Precompute the coefficients of tx(x), ty(y), and tz(z).
    // The octree is assumed to reside at coordinates [1, 2].
    res.dT = 1.0f / -abs(ray.direction);        //Negate all directions
    res.bT = res.dT * ray.origin;

    //Perform mirroring because we dont allow the ray`s components to be positive
    //but only perform mirroring for an axis if elevation is negative
    //Mirroring arround x,y,z = 1.5f (center of root voxel):
    // mirroredRay.origin = -(ray.origin - 1.5f) + 1.5f
    // mirroredRay.origin = -ray.origin + 3.0f
    // => mirroredbT = mirroredRay.origin * dT
    //    mirroredbT = -ray.origin * dT + 3.0f * dT
    //    mirroredbT = -bT + 3.0f * dT

    int octantMask = 0;
    if (ray.direction.x > 0.0f)
    {
        octantMask ^= 4;
        res.bT.x = 3.0f * res.dT.x - res.bT.x;
    }
    if (ray.direction.y > 0.0f)
    {
        octantMask ^= 2;
        res.bT.y = 3.0f * res.dT.y - res.bT.y;
    }
    if (ray.direction.z > 0.0f)
    {
        octantMask ^= 1;
        res.bT.z = 3.0f * res.dT.z - res.bT.z;
    }

    //Starting distance of ray in cube must be the biggest of the three axes
    float minT = max(2.0f * res.dT.x - res.bT.x, max(2.0f * res.dT.y - res.bT.y, 2.0f * res.dT.z - res.bT.z));
    //Maximal distance in cube must be the smallest of the three axes
    float maxT = min(res.dT.x - res.bT.x, min(res.dT.y - res.bT.y, res.dT.z - res.bT.z));
    
    res.minT = minT;
    res.maxT = maxT;

    //Remove behind the camera intersections (f.e. if we are already inside the space of [1,2])
    minT = max(res.minT, 0.0f);

    uint nodeIdx = startIndex;
    int node = 0;
    int idx = 0;
    float3 pos = 1;
    int scale = MAX_SCALE - 1;
    float scaleExp2 = 0.5f;

    //1.5f is the initial position of the planes of the axis
    //find the entry voxel of the initial 8 and also the initial idx offset
    if (1.5f * res.dT.x - res.bT.x > minT)
    {
        idx ^= 4;
        pos.x = 1.5f;
    }
    if (1.5f * res.dT.y - res.bT.y > minT)
    {
        idx ^= 2;
        pos.y = 1.5f;
    }
    if (1.5f * res.dT.z - res.bT.z > minT)
    {
        idx ^= 1;
        pos.z = 1.5f;
    }

    node = _octreeNodes[nodeIdx];

    //https://www.desmos.com/calculator/ow6qakorlc
    while (scale < MAX_SCALE)
    {
        //the distance of each axis to the bottom-left corner of the current child voxel
        float3 cornerT = pos * res.dT - res.bT;

        //smallest distance to reaching a corner plane of this voxel (where the ray will exit this child voxel)
        float maxTC = min(cornerT.x, min(cornerT.y, cornerT.z));

        //use mirroring (idx ^ octantMask) and set corresponding child bit
        int childBit = 1 << (idx ^ octantMask);
        int isValidNode = node & childBit;

        //if there is a voxel in this octant 
        //if the ray collides with this voxel
        if (isValidNode && minT <= maxT)
        {
            //push

            //Terminate ray if it is small enough
            if (minT * projFactor > scaleExp2)
                break;

            int childIndex = FetchChildIndex(_octreeNodes, nodeIdx, node, childBit);
            bool isLeafNode = scale == (MAX_SCALE - maxDepth);
                
            //bool isLeafNode = childNode == 0;
            if (isLeafNode)
            {
                //leaf node found
                //Return minT here 
                break;
            }

            //Store data on stack
            rayStack[scale].offset = nodeIdx;
            rayStack[scale].maxT = maxT;
            
            //Go one level deeper
            //half scale because we go one level deeper
            scale--;
            scaleExp2 *= .5f;

            //offset distances to voxel corner to get distance to center of the current child voxel
            float3 centerT = scaleExp2 * res.dT + cornerT;

            //Set parent to next child
            nodeIdx = childIndex;
            //Fetch child node
            node = _octreeNodes[nodeIdx];

            //find octant of next child which the ray enters first
            idx = 0;
            if (centerT.x > minT)
            {
                idx ^= 4;
                pos.x += scaleExp2;
            }
            if (centerT.y > minT)
            {
                idx ^= 2;
                pos.y += scaleExp2;
            }
            if (centerT.z > minT)
            {
                idx ^= 1;
                pos.z += scaleExp2;
            }

            //Set the max t of the current voxel clamped to maxT (maxT is the max t value of the bounding voxel)
            maxT = min(maxT, maxTC);

            //start on next level by fetching new child
            continue;
        }

        //ADVANCE
        //Advance in octants of this parent
        //Check which side will be hit first => then we know which voxel needs to be checked next
        //f.e. the x axis is intersected first, then we need to check the voxel on the other side of the axis
        int stepMask = 0;
        if (cornerT.x == maxTC)
        {
            stepMask ^= 4;
            pos.x -= scaleExp2;
        }
        if (cornerT.y == maxTC)
        {
            stepMask ^= 2;
            pos.y -= scaleExp2;
        }
        if (cornerT.z == maxTC)
        {
            stepMask ^= 1;
            pos.z -= scaleExp2;
        }

        //reduce maximal range to newly found subrange
        minT = maxTC;
        idx ^= stepMask;

        //Check if stepmask and idx have both bits set to 1
        // => This is the indication that we moved through all children available in this parent and we need to move back up again
        // Only works because we only allow negative components (see raytracing_visualization_tests.yml)
        if ((idx & stepMask) != 0)
        {
            //POP

            //Bits of IEEE float Exponent used to determine 
            int differingBits = 0;
            if ((stepMask & 4) != 0) differingBits |= asint(pos.x) ^ asint(pos.x + scaleExp2);
            if ((stepMask & 2) != 0) differingBits |= asint(pos.y) ^ asint(pos.y + scaleExp2);
            if ((stepMask & 1) != 0) differingBits |= asint(pos.z) ^ asint(pos.z + scaleExp2);

            //shift right 23 to remove mantisse bits
            scale = (asint((float) differingBits) >> 23) - 127;
            scaleExp2 = asfloat((scale - MAX_SCALE + 127) << 23);

            //due to the previous substraction of pos with scaleExp,
            //we remember to not again look into the same child again when leaving this child and returning to the parent
            //  => this saves us an extra variable (important cache memory!)
            //  f.e. scale_exp2 = .25f; pos = 1.5f (but with substraction in ADVANCE = 1.25f)
            //  1.5f = 1.0b1
            //  1.25f = 1.0b01
            //  0bXi..Xn visualize the mantisse bits from biggest to smallest in IEEEFloat
            //      => so we know we already visited voxel at 1.5f in a sample axis when returning back to highest level
            //  scale determines the mantisse bits to look at starting at 22 = highes, and 0 = lowest
            //      => shift position with scale to get bit we are interested in at lowest position
            //      => remove other data by ANDING with 1 (only interest in first bit as previously mentioned)
            //  determine index by shifting axis corresponding to voxel index mapping

            int3 sh = asint(pos) >> scale;
            pos = asfloat(sh << scale);         //Removes all lower decimal values than this voxel level requires
            idx = ((sh.x & 1) << 2) | ((sh.y & 1) << 1) | (sh.z & 1);

            nodeIdx = rayStack[scale].offset;
            maxT = rayStack[scale].maxT;

            //Fetch node from stack pos
            node = _octreeNodes[nodeIdx];
        }
    }

    res.t = minT;

    //Not hit anything
    if (scale >= MAX_SCALE)
    {
        res.hitPos = 0.0f;
        return;
    }

    //Undo mirroring
    //subtracting the center of the found voxel from 3 yields the unmirrored position
    if ((octantMask & 4) != 0) pos.x = 3.0f - scaleExp2 - pos.x;
    if ((octantMask & 2) != 0) pos.y = 3.0f - scaleExp2 - pos.y;
    if ((octantMask & 1) != 0) pos.z = 3.0f - scaleExp2 - pos.z;

    
    const float epsilon = exp2(-MAX_SCALE);
    res.hitPos = min(max(ray.origin + res.t * ray.direction, pos + epsilon), pos + scaleExp2 - epsilon);
    res.voxelPos = pos + scaleExp2 * .5f;
}

int GetDepth(const int scale)
{
    return MAX_SCALE - scale - 1;
}


int GetColorFromDepthNodeIdx(int childIndex, int depth, int4 _levelStartIndices[23])
{
    int startIdx = _levelStartIndices[depth].x;
    int idxInLevel = _levelStartIndices[depth+1].x - startIdx;

    //example data of sanmiguel 8192
    //startIdx = 96704348 + 23445793;
    //idxInLevel = 254 + 1020;
    
    int levelIdx = childIndex - startIdx;

    float normIdx = (float) levelIdx / (float) idxInLevel;
    int colorFullRange = (int) (normIdx * (1 << 24));
    
    return colorFullRange;
}


void TraceDagNodeHighlighting(StructuredBuffer<uint> _octreeNodes, int startIndex, int maxDepth, Ray ray, out TraceResult res, out int color, int4 _levelStartIndices[23])
{
    DepthData rayStack[MAX_SCALE];

    //clamp ray direction to minimal value (reduces floating point inacurracy)
    if (abs(ray.direction.x) < 1e-4f) ray.direction.x = 1e-4f;
    if (abs(ray.direction.y) < 1e-4f) ray.direction.y = 1e-4f;
    if (abs(ray.direction.z) < 1e-4f) ray.direction.z = 1e-4f;

    //Forumla for calculating the distance to a plane 
    //t = (lines - ray.origin) / ray.direction
    //t = (lines - ray.origin) * dT
    //t = lines * dT - ray.origin * dT              -> bT = ray.orgin * dT
    //t = lines * dT - bT

    // Precompute the coefficients of tx(x), ty(y), and tz(z).
    // The octree is assumed to reside at coordinates [1, 2].
    res.dT = 1.0f / -abs(ray.direction);        //Negate all directions
    res.bT = res.dT * ray.origin;

    //Perform mirroring because we dont allow the ray`s components to be positive
    //but only perform mirroring for an axis if elevation is negative
    //Mirroring arround x,y,z = 1.5f (center of root voxel):
    // mirroredRay.origin = -(ray.origin - 1.5f) + 1.5f
    // mirroredRay.origin = -ray.origin + 3.0f
    // => mirroredbT = mirroredRay.origin * dT
    //    mirroredbT = -ray.origin * dT + 3.0f * dT
    //    mirroredbT = -bT + 3.0f * dT

    int octantMask = 0;
    if (ray.direction.x > 0.0f)
    {
        octantMask ^= 4;
        res.bT.x = 3.0f * res.dT.x - res.bT.x;
    }
    if (ray.direction.y > 0.0f)
    {
        octantMask ^= 2;
        res.bT.y = 3.0f * res.dT.y - res.bT.y;
    }
    if (ray.direction.z > 0.0f)
    {
        octantMask ^= 1;
        res.bT.z = 3.0f * res.dT.z - res.bT.z;
    }

    //Starting distance of ray in cube must be the biggest of the three axes
    float minT = max(2.0f * res.dT.x - res.bT.x, max(2.0f * res.dT.y - res.bT.y, 2.0f * res.dT.z - res.bT.z));
    //Maximal distance in cube must be the smallest of the three axes
    float maxT = min(res.dT.x - res.bT.x, min(res.dT.y - res.bT.y, res.dT.z - res.bT.z));
    
    res.minT = minT;
    res.maxT = maxT;

    //Remove behind the camera intersections (f.e. if we are already inside the space of [1,2])
    minT = max(res.minT, 0.0f);

    uint nodeIdx = startIndex;
    int node = 0;
    int idx = 0;
    float3 pos = 1;
    int scale = MAX_SCALE - 1;
    float scaleExp2 = 0.5f;

    //1.5f is the initial position of the planes of the axis
    //find the entry voxel of the initial 8 and also the initial idx offset
    if (1.5f * res.dT.x - res.bT.x > minT)
    {
        idx ^= 4;
        pos.x = 1.5f;
    }
    if (1.5f * res.dT.y - res.bT.y > minT)
    {
        idx ^= 2;
        pos.y = 1.5f;
    }
    if (1.5f * res.dT.z - res.bT.z > minT)
    {
        idx ^= 1;
        pos.z = 1.5f;
    }

    node = _octreeNodes[nodeIdx];

    //https://www.desmos.com/calculator/ow6qakorlc
    while (scale < MAX_SCALE)
    {
        //the distance of each axis to the bottom-left corner of the current child voxel
        float3 cornerT = pos * res.dT - res.bT;

        //smallest distance to reaching a corner plane of this voxel (where the ray will exit this child voxel)
        float maxTC = min(cornerT.x, min(cornerT.y, cornerT.z));

        //use mirroring (idx ^ octantMask) and set corresponding child bit
        int childBit = 1 << (idx ^ octantMask);
        int isValidNode = node & childBit;

        //if there is a voxel in this octant 
        //if the ray collides with this voxel
        if (isValidNode && minT <= maxT)
        {
            //push

            //Terminate ray if it is small enough
            //if (scaleExp2 * projFactor > minT)
            //{
            //    res.t = minT;
            //    break;
            //}

            int childIndex = FetchChildIndex(_octreeNodes, nodeIdx, node, childBit);
            bool isLeafNode = scale == (MAX_SCALE - maxDepth);
                
            //bool isLeafNode = childNode == 0;
            if (isLeafNode)
            {
                color = GetColorFromDepthNodeIdx(nodeIdx, GetDepth(scale), _levelStartIndices);
                
                //leaf node found
                //Return minT here 
                break;
            }

            //Store data on stack
            rayStack[scale].offset = nodeIdx;
            rayStack[scale].maxT = maxT;
            
            //Go one level deeper
            //half scale because we go one level deeper
            scale--;
            scaleExp2 *= .5f;

            //offset distances to voxel corner to get distance to center of the current child voxel
            float3 centerT = scaleExp2 * res.dT + cornerT;

            //Set parent to next child
            nodeIdx = childIndex;
            //Fetch child node
            node = _octreeNodes[nodeIdx];

            //find octant of next child which the ray enters first
            idx = 0;
            if (centerT.x > minT)
            {
                idx ^= 4;
                pos.x += scaleExp2;
            }
            if (centerT.y > minT)
            {
                idx ^= 2;
                pos.y += scaleExp2;
            }
            if (centerT.z > minT)
            {
                idx ^= 1;
                pos.z += scaleExp2;
            }

            //Set the max t of the current voxel clamped to maxT (maxT is the max t value of the bounding voxel)
            maxT = min(maxT, maxTC);

            //start on next level by fetching new child
            continue;
        }

        //ADVANCE
        //Advance in octants of this parent
        //Check which side will be hit first => then we know which voxel needs to be checked next
        //f.e. the x axis is intersected first, then we need to check the voxel on the other side of the axis
        int stepMask = 0;
        if (cornerT.x == maxTC)
        {
            stepMask ^= 4;
            pos.x -= scaleExp2;
        }
        if (cornerT.y == maxTC)
        {
            stepMask ^= 2;
            pos.y -= scaleExp2;
        }
        if (cornerT.z == maxTC)
        {
            stepMask ^= 1;
            pos.z -= scaleExp2;
        }

        //reduce maximal range to newly found subrange
        minT = maxTC;
        idx ^= stepMask;

        //Check if stepmask and idx have both bits set to 1
        // => This is the indication that we moved through all children available in this parent and we need to move back up again
        // Only works because we only allow negative components (see raytracing_visualization_tests.yml)
        if ((idx & stepMask) != 0)
        {
            //POP

            //Bits of IEEE float Exponent used to determine 
            int differingBits = 0;
            if ((stepMask & 4) != 0) differingBits |= asint(pos.x) ^ asint(pos.x + scaleExp2);
            if ((stepMask & 2) != 0) differingBits |= asint(pos.y) ^ asint(pos.y + scaleExp2);
            if ((stepMask & 1) != 0) differingBits |= asint(pos.z) ^ asint(pos.z + scaleExp2);

            //shift right 23 to remove mantisse bits
            scale = (asint((float) differingBits) >> 23) - 127;
            scaleExp2 = asfloat((scale - MAX_SCALE + 127) << 23);

            //due to the previous substraction of pos with scaleExp,
            //we remember to not again look into the same child again when leaving this child and returning to the parent
            //  => this saves us an extra variable (important cache memory!)
            //  f.e. scale_exp2 = .25f; pos = 1.5f (but with substraction in ADVANCE = 1.25f)
            //  1.5f = 1.0b1
            //  1.25f = 1.0b01
            //  0bXi..Xn visualize the mantisse bits from biggest to smallest in IEEEFloat
            //      => so we know we already visited voxel at 1.5f in a sample axis when returning back to highest level
            //  scale determines the mantisse bits to look at starting at 22 = highes, and 0 = lowest
            //      => shift position with scale to get bit we are interested in at lowest position
            //      => remove other data by ANDING with 1 (only interest in first bit as previously mentioned)
            //  determine index by shifting axis corresponding to voxel index mapping

            int3 sh = asint(pos) >> scale;
            pos = asfloat(sh << scale);         //Removes all lower decimal values than this voxel level requires
            idx = ((sh.x & 1) << 2) | ((sh.y & 1) << 1) | (sh.z & 1);

            nodeIdx = rayStack[scale].offset;
            maxT = rayStack[scale].maxT;

            //Fetch node from stack pos
            node = _octreeNodes[nodeIdx];
        }
    }

    res.t = minT;

    //Not hit anything
    if (scale >= MAX_SCALE)
    {
        res.hitPos = 0.0f;
        return;
    }

    //Undo mirroring
    //subtracting the center of the found voxel from 3 yields the unmirrored position
    if ((octantMask & 4) != 0) pos.x = 3.0f - scaleExp2 - pos.x;
    if ((octantMask & 2) != 0) pos.y = 3.0f - scaleExp2 - pos.y;
    if ((octantMask & 1) != 0) pos.z = 3.0f - scaleExp2 - pos.z;

    
    const float epsilon = exp2(-MAX_SCALE);
    res.hitPos = min(max(ray.origin + res.t * ray.direction, pos + epsilon), pos + scaleExp2 - epsilon);
    res.voxelPos = pos + scaleExp2 * .5f;
}

void TraceDagNodeLODHighlighting(StructuredBuffer<uint> _octreeNodes, int startIndex, int maxDepth, Ray ray, out TraceResult res, float projFactor, out int iterationDepth)
{
    DepthData rayStack[MAX_SCALE];

    //clamp ray direction to minimal value (reduces floating point inacurracy)
    if (abs(ray.direction.x) < 1e-4f) ray.direction.x = 1e-4f;
    if (abs(ray.direction.y) < 1e-4f) ray.direction.y = 1e-4f;
    if (abs(ray.direction.z) < 1e-4f) ray.direction.z = 1e-4f;

    //Forumla for calculating the distance to a plane 
    //t = (lines - ray.origin) / ray.direction
    //t = (lines - ray.origin) * dT
    //t = lines * dT - ray.origin * dT              -> bT = ray.orgin * dT
    //t = lines * dT - bT

    // Precompute the coefficients of tx(x), ty(y), and tz(z).
    // The octree is assumed to reside at coordinates [1, 2].
    res.dT = 1.0f / -abs(ray.direction);        //Negate all directions
    res.bT = res.dT * ray.origin;

    //Perform mirroring because we dont allow the ray`s components to be positive
    //but only perform mirroring for an axis if elevation is negative
    //Mirroring arround x,y,z = 1.5f (center of root voxel):
    // mirroredRay.origin = -(ray.origin - 1.5f) + 1.5f
    // mirroredRay.origin = -ray.origin + 3.0f
    // => mirroredbT = mirroredRay.origin * dT
    //    mirroredbT = -ray.origin * dT + 3.0f * dT
    //    mirroredbT = -bT + 3.0f * dT

    int octantMask = 0;
    if (ray.direction.x > 0.0f)
    {
        octantMask ^= 4;
        res.bT.x = 3.0f * res.dT.x - res.bT.x;
    }
    if (ray.direction.y > 0.0f)
    {
        octantMask ^= 2;
        res.bT.y = 3.0f * res.dT.y - res.bT.y;
    }
    if (ray.direction.z > 0.0f)
    {
        octantMask ^= 1;
        res.bT.z = 3.0f * res.dT.z - res.bT.z;
    }

    //Starting distance of ray in cube must be the biggest of the three axes
    float minT = max(2.0f * res.dT.x - res.bT.x, max(2.0f * res.dT.y - res.bT.y, 2.0f * res.dT.z - res.bT.z));
    //Maximal distance in cube must be the smallest of the three axes
    float maxT = min(res.dT.x - res.bT.x, min(res.dT.y - res.bT.y, res.dT.z - res.bT.z));
    
    res.minT = minT;
    res.maxT = maxT;

    //Remove behind the camera intersections (f.e. if we are already inside the space of [1,2])
    minT = max(res.minT, 0.0f);

    uint nodeIdx = startIndex;
    int node = 0;
    int idx = 0;
    float3 pos = 1;
    int scale = MAX_SCALE - 1;
    float scaleExp2 = 0.5f;

    //1.5f is the initial position of the planes of the axis
    //find the entry voxel of the initial 8 and also the initial idx offset
    if (1.5f * res.dT.x - res.bT.x > minT)
    {
        idx ^= 4;
        pos.x = 1.5f;
    }
    if (1.5f * res.dT.y - res.bT.y > minT)
    {
        idx ^= 2;
        pos.y = 1.5f;
    }
    if (1.5f * res.dT.z - res.bT.z > minT)
    {
        idx ^= 1;
        pos.z = 1.5f;
    }

    node = _octreeNodes[nodeIdx];

    //https://www.desmos.com/calculator/ow6qakorlc
    while (scale < MAX_SCALE)
    {
        //the distance of each axis to the bottom-left corner of the current child voxel
        float3 cornerT = pos * res.dT - res.bT;

        //smallest distance to reaching a corner plane of this voxel (where the ray will exit this child voxel)
        float maxTC = min(cornerT.x, min(cornerT.y, cornerT.z));

        //use mirroring (idx ^ octantMask) and set corresponding child bit
        int childBit = 1 << (idx ^ octantMask);
        int isValidNode = node & childBit;

        //if there is a voxel in this octant 
        //if the ray collides with this voxel
        if (isValidNode && minT <= maxT)
        {
            //push
            //Terminate ray if it is small enough

            if (minT * projFactor > scaleExp2)
            {
                iterationDepth = MAX_SCALE - scale;
                res.t = minT;
                
                break;
            }
            
            int childIndex = FetchChildIndex(_octreeNodes, nodeIdx, node, childBit);
            bool isLeafNode = scale == (MAX_SCALE - maxDepth);
                
            //bool isLeafNode = childNode == 0;
            if (isLeafNode)
            {
                iterationDepth = MAX_SCALE - scale;
                res.t = minT;
                //leaf node found
                //Return minT here 
                break;
            }

            //Store data on stack
            rayStack[scale].offset = nodeIdx;
            rayStack[scale].maxT = maxT;
            
            //Go one level deeper
            //half scale because we go one level deeper
            scale--;
            scaleExp2 *= .5f;

            //offset distances to voxel corner to get distance to center of the current child voxel
            float3 centerT = scaleExp2 * res.dT + cornerT;

            //Set parent to next child
            nodeIdx = childIndex;
            //Fetch child node
            node = _octreeNodes[nodeIdx];

            //find octant of next child which the ray enters first
            idx = 0;
            if (centerT.x > minT)
            {
                idx ^= 4;
                pos.x += scaleExp2;
            }
            if (centerT.y > minT)
            {
                idx ^= 2;
                pos.y += scaleExp2;
            }
            if (centerT.z > minT)
            {
                idx ^= 1;
                pos.z += scaleExp2;
            }

            //Set the max t of the current voxel clamped to maxT (maxT is the max t value of the bounding voxel)
            maxT = min(maxT, maxTC);

            //start on next level by fetching new child
            continue;
        }

        //ADVANCE
        //Advance in octants of this parent
        //Check which side will be hit first => then we know which voxel needs to be checked next
        //f.e. the x axis is intersected first, then we need to check the voxel on the other side of the axis
        int stepMask = 0;
        if (cornerT.x == maxTC)
        {
            stepMask ^= 4;
            pos.x -= scaleExp2;
        }
        if (cornerT.y == maxTC)
        {
            stepMask ^= 2;
            pos.y -= scaleExp2;
        }
        if (cornerT.z == maxTC)
        {
            stepMask ^= 1;
            pos.z -= scaleExp2;
        }

        //reduce maximal range to newly found subrange
        minT = maxTC;
        idx ^= stepMask;

        //Check if stepmask and idx have both bits set to 1
        // => This is the indication that we moved through all children available in this parent and we need to move back up again
        // Only works because we only allow negative components (see raytracing_visualization_tests.yml)
        if ((idx & stepMask) != 0)
        {
            //POP

            //Bits of IEEE float Exponent used to determine 
            int differingBits = 0;
            if ((stepMask & 4) != 0) differingBits |= asint(pos.x) ^ asint(pos.x + scaleExp2);
            if ((stepMask & 2) != 0) differingBits |= asint(pos.y) ^ asint(pos.y + scaleExp2);
            if ((stepMask & 1) != 0) differingBits |= asint(pos.z) ^ asint(pos.z + scaleExp2);

            //shift right 23 to remove mantisse bits
            scale = (asint((float) differingBits) >> 23) - 127;
            scaleExp2 = asfloat((scale - MAX_SCALE + 127) << 23);

            //due to the previous substraction of pos with scaleExp,
            //we remember to not again look into the same child again when leaving this child and returning to the parent
            //  => this saves us an extra variable (important cache memory!)
            //  f.e. scale_exp2 = .25f; pos = 1.5f (but with substraction in ADVANCE = 1.25f)
            //  1.5f = 1.0b1
            //  1.25f = 1.0b01
            //  0bXi..Xn visualize the mantisse bits from biggest to smallest in IEEEFloat
            //      => so we know we already visited voxel at 1.5f in a sample axis when returning back to highest level
            //  scale determines the mantisse bits to look at starting at 22 = highes, and 0 = lowest
            //      => shift position with scale to get bit we are interested in at lowest position
            //      => remove other data by ANDING with 1 (only interest in first bit as previously mentioned)
            //  determine index by shifting axis corresponding to voxel index mapping

            int3 sh = asint(pos) >> scale;
            pos = asfloat(sh << scale);         //Removes all lower decimal values than this voxel level requires
            idx = ((sh.x & 1) << 2) | ((sh.y & 1) << 1) | (sh.z & 1);

            nodeIdx = rayStack[scale].offset;
            maxT = rayStack[scale].maxT;

            //Fetch node from stack pos
            node = _octreeNodes[nodeIdx];
        }
    }

    res.t = minT;

    //Not hit anything
    if (scale >= MAX_SCALE)
    {
        res.hitPos = 0.0f;
        return;
    }

    //Undo mirroring
    //subtracting the center of the found voxel from 3 yields the unmirrored position
    if ((octantMask & 4) != 0) pos.x = 3.0f - scaleExp2 - pos.x;
    if ((octantMask & 2) != 0) pos.y = 3.0f - scaleExp2 - pos.y;
    if ((octantMask & 1) != 0) pos.z = 3.0f - scaleExp2 - pos.z;

    
    const float epsilon = exp2(-MAX_SCALE);
    res.hitPos = min(max(ray.origin + res.t * ray.direction, pos + epsilon), pos + scaleExp2 - epsilon);
    res.voxelPos = pos + scaleExp2 * .5f;
}


bool TraceDagStreaming(StructuredBuffer<uint> _octreeNodes, int startIndex, int maxDepth, Ray ray, out TraceResult res, float projFactor, out int outOctMask)
{
    DepthData rayStack[MAX_SCALE + 1];

    //float3 ray.origin = ray.origin;
    //float3 ray.direction = ray.direction;

    if (abs(ray.direction.x) < 1e-4f)
        ray.direction.x = 1e-4f;
    if (abs(ray.direction.y) < 1e-4f)
        ray.direction.y = 1e-4f;
    if (abs(ray.direction.z) < 1e-4f)
        ray.direction.z = 1e-4f;

    // Precompute the coefficients of tx(x), ty(y), and tz(z).
    // The octree is assumed to reside at coordinates [1, 2].

    res.dT = 1.0f / -abs(ray.direction);
    res.bT = res.dT * ray.origin;

    //Perform mirroring because we dont allow the ray`s components to be positive
    int octantMask = 0;
    if (ray.direction.x > 0.0f)
    {
        octantMask ^= 4;
        res.bT.x = 3.0f * res.dT.x - res.bT.x;
    }
    if (ray.direction.y > 0.0f)
    {
        octantMask ^= 2;
        res.bT.y = 3.0f * res.dT.y - res.bT.y;
    }
    if (ray.direction.z > 0.0f)
    {
        octantMask ^= 1;
        res.bT.z = 3.0f * res.dT.z - res.bT.z;
    }

    res.minT = max(2.0f * res.dT.x - res.bT.x, max(2.0f * res.dT.y - res.bT.y, 2.0f * res.dT.z - res.bT.z));
    res.maxT = min(res.dT.x - res.bT.x, min(res.dT.y - res.bT.y, res.dT.z - res.bT.z));
    //Remove behind the camera intersections
    res.minT = max(res.minT, 0.0f);

    float maxT = res.maxT;
    float minT = res.minT;

    // t = ray.direction * x - b
    // t + b = ray.direction * x
    // (t+b)/ray.direction = x

    uint nodeIdx = startIndex;
    int node = 0;
    int idx = 0;
    float3 pos = 1;
    int scale = MAX_SCALE - 1;

    float scaleExp2 = 0.5f;

    //1.5f is the position of the planes of the axis
    if (1.5f * res.dT.x - res.bT.x > minT)
    {
        idx ^= 4;
        pos.x = 1.5f;
    }
    if (1.5f * res.dT.y - res.bT.y > minT)
    {
        idx ^= 2;
        pos.y = 1.5f;
    }
    if (1.5f * res.dT.z - res.bT.z > minT)
    {
        idx ^= 1;
        pos.z = 1.5f;
    }

    while (scale < MAX_SCALE)
    {
        if (node == 0)
            node = _octreeNodes[nodeIdx];

        float3 cornerT = pos * res.dT - res.bT;

        //minium t value to corner planes
        float maxTC = min(cornerT.x, min(cornerT.y, cornerT.z));

        int childShift = idx ^ octantMask;
        int childBit = 1 << childShift;
        //int isValidNode = node & childBit;

        if ((node & childBit)  && minT <= maxT)
        {
            //push

            //Terminate ray if it is small enough
            if (scaleExp2 * projFactor > minT)
            {
                res.t = minT;
                break;
            }

            //Set the max t of the current voxel clamped to maxT (maxT is the max t value of the bounding voxel)
            float maxTV = min(maxT, maxTC);

            //Checks if the first intersection of the ray (res.minT = entrance distance of the voxel) is before the second (maxTV = exit distance of the voxel)
            if (minT <= maxTV)
            {
                int childIndex = FetchChildIndex(_octreeNodes, nodeIdx, node, childBit);
                bool isLeafNode = scale == (MAX_SCALE - maxDepth);
                
                //bool isLeafNode = childNode == 0;
                if (isLeafNode)
                {
                    //leaf node found
                    //Return minT here 
                    res.t = minT;
                    break;
                }

                rayStack[scale].offset = nodeIdx;
                rayStack[scale].maxT = maxT;


                float halfScale = scaleExp2 * 0.5f;
                float3 centerT = halfScale * res.dT + cornerT;

                //Set parent to next child
                nodeIdx = childIndex;

                //Reset current node so that it will be fetched again at the start of the loop
                node = 0;

                idx = 0;
                scale--;
                scaleExp2 = halfScale;

                //find octant of next child which the ray enters first
                if (centerT.x > minT)
                {
                    idx ^= 4;
                    pos.x += scaleExp2;
                }
                if (centerT.y > minT)
                {
                    idx ^= 2;
                    pos.y += scaleExp2;
                }
                if (centerT.z > minT)
                {
                    idx ^= 1;
                    pos.z += scaleExp2;
                }

                maxT = maxTV;
                node = 0;

                continue;
            }
        }

        //Advance in octants of this parent

        int stepMask = 0;
        if (cornerT.x <= maxTC)
        {
            stepMask ^= 4;
            pos.x -= scaleExp2;
        }
        if (cornerT.y <= maxTC)
        {
            stepMask ^= 2;
            pos.y -= scaleExp2;
        }
        if (cornerT.z <= maxTC)
        {
            stepMask ^= 1;
            pos.z -= scaleExp2;
        }

        minT = maxTC;
        idx ^= stepMask;

        if ((idx & stepMask) != 0)
        {
            //Pop
            //Move one level up

            //Bits of IEEE float Exponent used to determine 
            uint differingBits = 0;
            if ((stepMask & 4) != 0)
                differingBits |= asint(pos.x) ^ asint(pos.x + scaleExp2);
            if ((stepMask & 2) != 0)
                differingBits |= asint(pos.y) ^ asint(pos.y + scaleExp2);
            if ((stepMask & 1) != 0)
                differingBits |= asint(pos.z) ^ asint(pos.z + scaleExp2);

            //shift right 23 to remove mantisse bits
            scale = (asint((float) differingBits) >> 23) - 127;
            scaleExp2 = asfloat((scale - MAX_SCALE + 127) << 23);

            nodeIdx = rayStack[scale].offset;
            maxT = rayStack[scale].maxT;

            int3 sh = asint(pos) >> scale;
            pos = asfloat(sh << scale);
            idx = ((sh.x & 1) << 2) | ((sh.y & 1) << 1) | (sh.z & 1);

            node = 0;
        }
    }

    //Not hit anything
    if (scale >= MAX_SCALE)
    {
        res.t = minT;
    }

    //Undo mirroring
    if ((octantMask & 4) != 0)
        pos.x = 3.0f - scaleExp2 - pos.x;
    if ((octantMask & 2) != 0)
        pos.y = 3.0f - scaleExp2 - pos.y;
    if ((octantMask & 1) != 0)
        pos.z = 3.0f - scaleExp2 - pos.z;

    const float epsilon = exp2(-MAX_SCALE);
    //const float epsilon = 1e-3;
    res.hitPos.x = min(max(ray.origin.x + res.t * ray.direction.x, pos.x + epsilon), pos.x + scaleExp2 - epsilon);
    res.hitPos.y = min(max(ray.origin.y + res.t * ray.direction.y, pos.y + epsilon), pos.y + scaleExp2 - epsilon);
    res.hitPos.z = min(max(ray.origin.z + res.t * ray.direction.z, pos.z + epsilon), pos.z + scaleExp2 - epsilon);

    //res.hitPos.x = min(max(ray.origin.x + res.t * ray.direction.x - epsilon, 1) + epsilon, 2);
    //res.hitPos.y = min(max(ray.origin.y + res.t * ray.direction.y - epsilon, 1) + epsilon, 2);
    //res.hitPos.z = min(max(ray.origin.z + res.t * ray.direction.z - epsilon, 1) + epsilon, 2);

    res.voxelPos = pos + scaleExp2 * .5f;
    
    outOctMask = idx;

    //Not hit anything
    return scale < MAX_SCALE;
}
