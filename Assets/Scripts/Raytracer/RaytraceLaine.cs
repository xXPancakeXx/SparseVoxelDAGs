#define MY_DEBUG


using Assets.Scripts.Dag;
using Assets.Scripts.Entities;
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Ray = Assets.Scripts.Entities.Ray;

namespace Assets.Scripts.Raytracer
{
    public partial class CPURaytracer
    {
        private const int MAX_SCALE = 23;

        private static unsafe int FetchChildIndex(int* data, uint parentIndex, int node, int childBit)
        {
            int lowerIndexLeafOctantMask = childBit - 1;

            //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)
            int childIndexOffset = math.countbits((uint)(node & lowerIndexLeafOctantMask)) + 1;
            int childRelativePtr = data[parentIndex + childIndexOffset];
            int childIndex = (int)(parentIndex + childIndexOffset + childRelativePtr);

            return childIndex;
        }

        private static unsafe void TraceDag(int* _octreeNodes, int startIndex, int maxDepth, Ray ray, out TraceResultColor res, float projFactor)
        {
            res = new TraceResultColor();
            var rayStack = stackalloc DepthData[MAX_SCALE];

            //clamp ray direction to minimal value (reduces floating point inacurracy)
            if (math.abs(ray.direction.x) < 1e-4f) ray.direction.x = 1e-4f;
            if (math.abs(ray.direction.y) < 1e-4f) ray.direction.y = 1e-4f;
            if (math.abs(ray.direction.z) < 1e-4f) ray.direction.z = 1e-4f;

            //Forumla for calculating the distance to a plane 
            //t = (lines - ray.origin) / ray.direction
            //t = (lines - ray.origin) * dT
            //t = lines * dT - ray.origin * dT              -> bT = ray.orgin * dT
            //t = lines * dT - bT

            // Precompute the coefficients of tx(x), ty(y), and tz(z).
            // The octree is assumed to reside at coordinates [1, 2].
            res.dT = 1.0f / -math.abs(ray.direction);        //Negate all directions
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
            float minT = math.max(2.0f * res.dT.x - res.bT.x, math.max(2.0f * res.dT.y - res.bT.y, 2.0f * res.dT.z - res.bT.z));
            //Maximal distance in cube must be the smallest of the three axes
            float maxT = math.min(res.dT.x - res.bT.x, math.min(res.dT.y - res.bT.y, res.dT.z - res.bT.z));

            res.minT = minT;
            res.maxT = maxT;

            //Remove behind the camera intersections (f.e. if we are already inside the space of [1,2])
            minT = math.max(res.minT, 0.0f);

            // t = ray.direction * x - b
            // t + b = ray.direction * x
            // (t+b)/ray.direction = x

            int parentIndex = 0;
            int currentNode = 0;
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


            //https://www.desmos.com/calculator/ow6qakorlc

            while (scale < MAX_SCALE)
            {
                if (currentNode == 0) currentNode = _octreeNodes[parentIndex];

                //the distance of each axis to the corner of the current child voxel
                float3 cornerT = pos * res.dT - res.bT;

                //smallest distance to reaching a corner plane of this voxel
                float maxTC = math.min(cornerT.x, math.min(cornerT.y, cornerT.z));

                //use mirroring (idx ^ octantMask) and set corresponding child bit
                int childBit = 1 << (idx ^ octantMask);
                int isValidNode = currentNode & childBit;

                //if there is a voxel in this octant 
                //if the ray collides with this voxel
                if (isValidNode != 0 && minT <= maxT)
                {
                    //push

                    //Terminate ray if it is small enough
                    //if (scaleExp2 * projFactor > minT)
                    //{
                    //    res.t = minT;
                    //    break;
                    //}

                    int childIndex = FetchChildIndex(_octreeNodes, (uint)parentIndex, currentNode, childBit);
                    bool isLeafNode = scale == (MAX_SCALE - maxDepth);

                    //bool isLeafNode = childNode == 0;
                    if (isLeafNode)
                    {
                        //leaf node found
                        //Return minT here 
                        res.t = minT;
                        break;
                    }

                    rayStack[scale].offset = parentIndex;
                    rayStack[scale].maxT = maxT;

                    //Reset current node so that it will be fetched again at the start of the loop
                    currentNode = 0;

                    //Go one level deeper
                    //half scale because we go one level deeper
                    scale--;
                    scaleExp2 *= .5f;

                    //offset distances to voxel corner to get distance to center of the current child voxel
                    float3 centerT = scaleExp2 * res.dT + cornerT;

                    //Set parent to next child
                    parentIndex = childIndex;

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
                    maxT = math.min(maxT, maxTC);

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
                    //this is able to pop multiple levels depending on the edge of the voxel and all its parents


                    //Bits of IEEE float Exponent used to determine 
                    int differingBits = 0;
                    if ((stepMask & 4) != 0) differingBits |= math.asint(pos.x) ^ math.asint(pos.x + scaleExp2);
                    if ((stepMask & 2) != 0) differingBits |= math.asint(pos.y) ^ math.asint(pos.y + scaleExp2);
                    if ((stepMask & 1) != 0) differingBits |= math.asint(pos.z) ^ math.asint(pos.z + scaleExp2);

                    //shift right 23 to remove mantisse bits
                    scale = (math.asint((float)differingBits) >> 23) - 127;
                    scaleExp2 = math.asfloat((scale - MAX_SCALE + 127) << 23);

                    parentIndex = rayStack[scale].offset;
                    maxT = rayStack[scale].maxT;

                    //due to the previous substraction of pos with scaleExp,
                    //we remember to not again look into the same child again when leaving this child and returning to the parent
                    //  => this saves us an extra variable (important cache memory!)
                    //  f.e. scale_exp2 = .25f; pos = 1.5f (but with substraction in ADVANCE = 1.25f)
                    //  1.5f = 1.0b1
                    //  1.25f = 1.0b01
                    //  0bXi..Xn visualize the mantisse bits from biggest to smallest in IEEEFloat
                    //      => so we know we already visited voxel at 1.5f in a sample axis when returning back to highest level
                    //  scale determines the mantisse bits to look at starting at 22 = highes, and 0 = lowest
                    //      => shift position with scale to get bit we are interested at lowest position
                    //      => remove other data by ANDING with 1 (only interest in first bit as previously mentioned)
                    //  determine index by shifting axis corresponding to voxel index mapping

                    int3 sh = math.asint(pos) >> scale;
                    pos = math.asfloat(sh << scale);         //Removes all lower decimal values than this voxel level requires
                    idx = ((sh.x & 1) << 2) | ((sh.y & 1) << 1) | (sh.z & 1);

                    //Reset node, for refetching of parentNode
                    currentNode = 0;
                }
            }

            //Not hit anything
            if (scale >= MAX_SCALE)
            {
                res.t = minT;
                res.hitPos = 0.0f;
                return;
            }

            //Undo mirroring
            //subtracting the center of the found voxel from 3 yield the unmirrored position
            if ((octantMask & 4) != 0) pos.x = 3.0f - scaleExp2 - pos.x;
            if ((octantMask & 2) != 0) pos.y = 3.0f - scaleExp2 - pos.y;
            if ((octantMask & 1) != 0) pos.z = 3.0f - scaleExp2 - pos.z;

            float epsilon = math.exp2(-MAX_SCALE);
            res.hitPos = math.min(math.max(ray.origin + res.t * ray.direction, pos + epsilon), pos + scaleExp2 - epsilon);
            res.voxelPos = pos + scaleExp2 * .5f;
        }


        private bool IsPenultimateLevel(int scale, int maxDepth)
        {
            //maxscale = 23
            //maxdepth = 2
            //minscale = 21
            //init scale = 22

            return (scale - 1) == (MAX_SCALE - maxDepth);
        }

        private unsafe uint FetchChildIndex(int* data, uint parentIndex, int node, int childBit, bool isPenultimateLevel)
        {
            int lowerIndexLeafOctantMask = childBit - 1;

            //zero out all higher bits than octantId (& op) and count bits which are 1 (tells us how many octants are before this one = offset)
            int childIndexOffset = math.countbits((uint)(node & lowerIndexLeafOctantMask));
            childIndexOffset += 2; //childIndexOffset ranges from [0,7] but we require [1,8] (+1)

            //if (!isPenultimateLevel)
            //{
            //    childIndexOffset++; //because our node headers are double the size of a node (+1) on all levels expect the penultimate one
            //}

            int childRelativePtr = data[parentIndex + childIndexOffset];
            uint childIndex = (uint)(parentIndex + childIndexOffset + childRelativePtr);

            return childIndex;

        }

        private unsafe uint SumPrecedingChildrenSubtreeCount(int* data, uint parentIndex, int node, int childBit, bool isLeafNode)
        {
            if (isLeafNode)
            {
                int lowerIndexLeafOctantMask = childBit - 1;

                return (uint) math.countbits(node & lowerIndexLeafOctantMask) + 1;
            }

            //go through all children before the one passed
            uint sum = 0;
            while ((childBit >>= 1) != 0)
            {
                bool hasChild = (childBit & node) != 0;
                if (hasChild)
                {
                    uint childIndex = FetchChildIndex(data, parentIndex, node, childBit, false);
                    //int childSubtreeCount = data[childIndex+1];

                    int childValidMask = data[childIndex];
                    int lowerIndexLeafOctantMask = childBit - 1;
                    //return countbits(node & lowerIndexLeafOctantMask);

                    sum += (uint) math.countbits(childValidMask);
                }
            }

            return sum;
        }

        public unsafe void TraceDag(int* _octreeNodes, int maxDepth, Ray ray, out TraceResultColor res, float projFactor)
        {
            DepthDataDAGColor[] rayStack = new DepthDataDAGColor[MAX_SCALE + 1];

            float3 o = ray.origin;
            float3 d = ray.direction;

            if (math.abs(d.x) < 1e-4f) d.x = 1e-4f;
            if (math.abs(d.y) < 1e-4f) d.y = 1e-4f;
            if (math.abs(d.z) < 1e-4f) d.z = 1e-4f;

            // Precompute the coefficients of tx(x), ty(y), and tz(z).
            // The octree is assumed to reside at coordinates [1, 2].

            res.dT = 1.0f / -math.abs(d);
            res.bT = res.dT * o;

            //Perform mirroring because we dont allow the ray`s components to be positive
            int octantMask = 0;
            if (d.x > 0.0f)
            {
                octantMask ^= 4;
                res.bT.x = 3.0f * res.dT.x - res.bT.x;
            }
            if (d.y > 0.0f)
            {
                octantMask ^= 2;
                res.bT.y = 3.0f * res.dT.y - res.bT.y;
            }
            if (d.z > 0.0f)
            {
                octantMask ^= 1;
                res.bT.z = 3.0f * res.dT.z - res.bT.z;
            }

            res.minT = math.max(2.0f * res.dT.x - res.bT.x, math.max(2.0f * res.dT.y - res.bT.y, 2.0f * res.dT.z - res.bT.z));
            res.maxT = math.min(res.dT.x - res.bT.x, math.min(res.dT.y - res.bT.y, res.dT.z - res.bT.z));
            //Remove behind the camera intersections
            res.minT = math.max(res.minT, 0.0f);

            float maxT = res.maxT;
            float minT = res.minT;

            // t = d * x - b
            // t + b = d * x
            // (t+b)/d = x

            uint attributeIndex = 0;

            uint parentIndex = 0;
            int currentNode = 0;
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

            res.t = minT;
            while (scale < MAX_SCALE)
            {
                if (currentNode == 0)
                    currentNode = _octreeNodes[parentIndex];

                float3 cornerT = pos * res.dT - res.bT;

                //minium t value to corner planes
                float maxTC = math.min(cornerT.x, math.min(cornerT.y, cornerT.z));

                int childShift = idx ^ octantMask;
                int childBit = 1 << childShift;
                int isValidNode = currentNode & childBit;

                if (isValidNode != 0 && minT <= maxT)
                {
                    //push

                    //Terminate ray if it is small enough
                    if (scaleExp2 * projFactor > minT)
                    {
                        res.t = minT;
                        break;
                    }

                    //Set the max t of the current voxel clamped to maxT (maxT is the max t value of the bounding voxel)
                    float maxTV = math.min(maxT, maxTC);

                    //Checks if the first intersection of the ray (res.minT = entrance distance of the voxel) is before the second (maxTV = exit distance of the voxel)
                    if (minT <= maxTV)
                    {
                        bool isLeafNode = scale == (MAX_SCALE - maxDepth);

                        uint attOffset = SumPrecedingChildrenSubtreeCount(_octreeNodes, parentIndex, currentNode, childBit, isLeafNode); //+1 for the node itsself
                        uint childIndex = FetchChildIndex(_octreeNodes, parentIndex, currentNode, childBit, IsPenultimateLevel(scale, maxDepth));

                        if (isLeafNode)
                        {
                            //leaf node found
                            //Return minT here 
                            res.t = minT;
                            res.colorIndex = (int)(attributeIndex + attOffset);

                            break;
                        }


                        rayStack[scale].offset = parentIndex;
                        rayStack[scale].maxT = maxT;
                        rayStack[scale].attributeIndex = attributeIndex;

                        float halfScale = scaleExp2 * 0.5f;
                        float3 centerT = halfScale * res.dT + cornerT;

                        //Set parent to next child
                        parentIndex = childIndex;
                        attributeIndex += attOffset;

                        //Reset current node so that it will be fetched again at the start of the loop
                        currentNode = 0;

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
                        currentNode = 0;

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
                    if ((stepMask & 4) != 0) differingBits |= (uint) (math.asint(pos.x) ^ math.asint(pos.x + scaleExp2));
                    if ((stepMask & 2) != 0) differingBits |= (uint) (math.asint(pos.y) ^ math.asint(pos.y + scaleExp2));
                    if ((stepMask & 1) != 0) differingBits |= (uint) (math.asint(pos.z) ^ math.asint(pos.z + scaleExp2));


                    //shift right 23 to remove mantisse bits
                    scale = (math.asint((float)differingBits) >> 23) - 127;
                    scaleExp2 = math.asfloat((scale - MAX_SCALE + 127) << 23);

                    parentIndex = rayStack[scale].offset;
                    maxT = rayStack[scale].maxT;
                    attributeIndex = rayStack[scale].attributeIndex;

                    int3 sh = math.asint(pos) >> scale;
                    pos = math.asfloat(sh << scale);
                    idx = ((sh.x & 1) << 2) | ((sh.y & 1) << 1) | (sh.z & 1);

                    currentNode = 0;
                }
            }

            //Not hit anything
            if (scale >= MAX_SCALE)
            {
                res.t = 2.0f;
                res.hitPos = 0.0f;
                res = default;
                return;
            }

            //Undo mirroring
            if ((octantMask & 4) != 0) pos.x = 3.0f - scaleExp2 - pos.x;
            if ((octantMask & 2) != 0) pos.y = 3.0f - scaleExp2 - pos.y;
            if ((octantMask & 1) != 0) pos.z = 3.0f - scaleExp2 - pos.z;

            float epsilon = math.exp2(-MAX_SCALE);
            res.hitPos.x = math.min(math.max(ray.origin.x + res.t * ray.direction.x, pos.x + epsilon), pos.x + scaleExp2 - epsilon);
            res.hitPos.y = math.min(math.max(ray.origin.y + res.t * ray.direction.y, pos.y + epsilon), pos.y + scaleExp2 - epsilon);
            res.hitPos.z = math.min(math.max(ray.origin.z + res.t * ray.direction.z, pos.z + epsilon), pos.z + scaleExp2 - epsilon);

            res.voxelPos = pos + scaleExp2 * .5f;
            res = default;
        }

    }
}