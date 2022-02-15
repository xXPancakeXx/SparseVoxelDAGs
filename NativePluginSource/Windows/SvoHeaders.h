#pragma once
#include <cstdint>
#include <vector>

struct SvoHeaderGray {
	uint32_t maxDepth;
	std::vector<int> nodesPerLevel;

	SvoHeaderGray(const char* filePath)
	{
		auto* file = f::OpenRead(filePath);
		Read(file);
		fclose(file);
	}

	void Read(FILE* file) {
		int format = 0;
		fread(&format, sizeof(int32_t), 1, file);
		fread(&maxDepth, sizeof(int32_t), 1, file);
		for (int i = 0; i < maxDepth; i++)
		{
			int levelCount = 0;
			fread(&levelCount, sizeof(uint32_t), 1, file);
			nodesPerLevel.push_back(levelCount);
		}
	}

	uint32_t GetNodeByteSize()
	{
		uint32_t nodes = 0;
		for (size_t i = 0; i < nodesPerLevel.size(); i++)
		{
			nodes += nodesPerLevel[i];
		}

		return nodes * 8u;
	}

	uint32_t GetDepthOffsetByte(int depth)
	{
		uint32_t nodes = 0;
		for (size_t i = 0; i < depth; i++)
		{
			nodes += nodesPerLevel[i];
		}
		return GetHeaderByteSize() + nodes * 8u;
	}

	uint32_t GetDepthStartIndex(int depth)
	{
		auto nodes = 0;
		for (size_t i = 0; i < depth; i++)
		{
			nodes += nodesPerLevel[i];
		}
		return nodes;
	}

	uint32_t GetGridDimension()
	{
		return 1 << maxDepth;
	}

	int GetHeaderByteSize() {
		return 4 * (
			1   //for the format 
			+ 1 //for maxdepth 
			+ nodesPerLevel.size()
		);
	}
};

struct SvoHeaderColorFull {
	uint32_t maxDepth;
	uint32_t colorCount;
	std::vector<int> nodesPerLevel;

	SvoHeaderColorFull(const char* filePath)
	{
		auto* file = f::OpenRead(filePath);
		Read(file);
		fclose(file);
	}

	void Read(FILE* file) {
		int format = 0;
		fread(&format, sizeof(int32_t), 1, file);
		fread(&maxDepth, sizeof(int32_t), 1, file);
		fread(&colorCount, sizeof(int32_t), 1, file);

		for (size_t i = 0; i < maxDepth + 1; i++)
		{
			int levelCount = 0;
			fread(&levelCount, sizeof(uint32_t), 1, file);
			nodesPerLevel.push_back(levelCount);
		}
	}

	uint32_t GetNodeCount()
	{
		auto nodes = 0;
		for (size_t i = 0; i < maxDepth + 1; i++)
		{
			nodes += nodesPerLevel[i];
		}
		return nodes;
	}

	
	uint32_t GetColorByteSize()
	{
		return colorCount * 4u;
	}

	uint32_t GetColorOffsetByte()
	{
		auto nodes = 0;
		for (size_t i = 0; i < maxDepth + 1; i++)
		{
			nodes += nodesPerLevel[i];
		}
		return GetHeaderByteSize() + (uint32_t)nodes * 8u;
	}

	uint32_t GetNodeByteSize()
	{
		auto nodes = 0;
		for (size_t i = 0; i < nodesPerLevel.size(); i++)
		{
			nodes += nodesPerLevel[i];
		}

		return (uint32_t)(nodes * 8u);
	}

	uint32_t GetDepthOffsetByte(int depth)
	{
		auto nodes = 0;
		for (size_t i = 0; i < depth; i++)
		{
			nodes += nodesPerLevel[i];
		}
		return GetHeaderByteSize() + (uint32_t)nodes * 8u;
	}

	uint32_t GetDepthStartIndex(int depth)
	{
		auto nodes = 0;
		for (size_t i = 0; i < depth; i++)
		{
			nodes += nodesPerLevel[i];
		}
		return nodes;
	}

	uint32_t GetGridDimension()
	{
		return 1 << maxDepth;
	}

	uint32_t GetHeaderByteSize() {
		return 4 * (
			1   //for the format 
			+ 1 //for maxdepth 
			+ 1 //for colorcount 
			+ nodesPerLevel.size()
			);
	}
};
