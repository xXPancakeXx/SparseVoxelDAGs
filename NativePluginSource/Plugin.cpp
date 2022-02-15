#include "Windows/DagBuilderGray.h"
#include "Windows/DagBuilderColorNode.h"
#include "Windows/f.h"
#include "Windows/DagHeaders.h"
#include "Windows/NativeLocalMesh.h"

#if _MSC_VER // this is defined when compiling with Visual Studio
#define EXPORT_API __declspec(dllexport) // Visual Studio needs annotating exported functions with this
#else
#define EXPORT_API // XCode does not need annotating exported functions, so define is empty
#endif

// ------------------------------------------------------------------------
// Plugin itself


// Link following functions C-style (required for plugins)
extern "C"
{

// The functions we will call from Unity.
//


EXPORT_API void ConvertToDag(char* svoPath, char* dagPath, char* modelName, int format, int depth) 
{
	FILE* file = f::OpenRead(svoPath);
	
    switch (format)
    {
        case DagFormat::Gray:
        {
            DagBuilderGray builder(svoPath, dagPath, modelName, depth);
            builder.Convert();
            break;
        }
        case DagFormat::ColorPerNode:
        {
            DagBuilderColorNode builder(svoPath, dagPath, modelName, depth);
            builder.Convert();
            break;
        }
    }
    
	fclose(file);
}

EXPORT_API void Voxelize(NativeLocalMesh* mesh)
{

}

} // end of export C block
