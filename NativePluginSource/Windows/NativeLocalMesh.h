#pragma once
//#include "hlslpp/hlsl++.h"
#include <vector>

//using namespace hlslpp;
//
//struct NativeLocalTexture
//{
//    std::vector<int> pixels;
//    int1 width, height;
//    bool isCreated;
//
//    NativeLocalTexture() :isCreated(false) {}
//
//    /*NativeLocalTexture(Texture2D tex)
//    {
//        this->width = tex.width;
//        this->height = tex.height;
//
//        var px = tex.GetPixels();
//        pixels = new UnsafeList<Color32>(width * height, Allocator.Persistent);
//        for (int i = 0; i < px.Length; i++)
//        {
//            pixels.Add(new Color(px[i].r, px[i].g, px[i].b, 1.0f));
//        }
//    }*/
//
//    int GetPixelBilinear(float1 u, float1 v)
//    {
//        int xMin, xMax, yMin, yMax;
//        float1 xfloat, yfloat;
//        int c, h1, h2;
//
//        xfloat = (width - 1) * frac(u);
//        yfloat = (height - 1) * frac(v);
//
//        xMin = (int)floor(xfloat);
//        xMax = (int)ceil(xfloat);
//
//        yMin = (int)floor(yfloat);
//        yMax = (int)ceil(yfloat);
//
//        h1 = lerp(GetPixel(xMin, yMin), GetPixel(xMax, yMin), frac(xfloat));
//        h2 = lerp(GetPixel(xMin, yMax), GetPixel(xMax, yMax), frac(xfloat));
//        c = lerp(h1, h2, frac(yfloat));
//        return c;
//    }
//
//    int GetPixel(int1 x, int1 y)
//    {
//        x = myClamp(x, 0, width - 1);
//        y = myClamp(y, 0, height - 1);
//        return pixels[x + y * width];
//    }
//
//    int1 myClamp(int1 x, int1 a, int1 b) {
//        return max(a, min(b, x));
//    }
//};
//
//struct NativeLocalMaterial {
//    NativeLocalTexture mainTex;
//    int albedo;
//
//    int Sample(float2 uv)
//    {
//        if (mainTex.isCreated) return mainTex.GetPixelBilinear(uv.x, uv.y);
//        return albedo;
//    }
//};


//struct NativeLocalMesh
//{
//    float3* vertices;
//    int* triangles;
//    int* vertexColors;
//    //float2* uvs;
//    //NativeLocalMaterial* materials;
//    //short* vertexMaterialMap;
//};