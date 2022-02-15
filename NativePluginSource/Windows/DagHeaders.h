#pragma once
#include <vector>
#include "SvoHeaders.h"
#include <execution>

enum DagFormat { Gray = 0, ColorPerPointer = 1, ColorPerNode = 2 };

struct DagHeaderGray {
	const int version = 1;
	const int format = DagFormat::Gray;
	int maxDepth;

	std::vector<uint32_t> nodeCountPerDepth;
	std::vector<uint32_t> childrenCountPerDepth;

	DagHeaderGray() {}

	DagHeaderGray(SvoHeaderGray& header) : maxDepth(header.maxDepth), nodeCountPerDepth(header.maxDepth), childrenCountPerDepth(header.maxDepth) {}

	void Write(FILE* file)
	{
		fwrite(&version, sizeof(uint32_t), 1, file);
		fwrite(&format, sizeof(uint32_t), 1, file);

		fwrite(&maxDepth, sizeof(uint32_t), 1, file);

		auto nodeCount = std::accumulate(nodeCountPerDepth.begin(), nodeCountPerDepth.end(), 0);
		auto childrenCount = std::accumulate(childrenCountPerDepth.begin(), childrenCountPerDepth.end(), 0);

		fwrite(&nodeCount, sizeof(uint32_t), 1, file);
		fwrite(&childrenCount, sizeof(uint32_t), 1, file);

		for (int i = 0; i < maxDepth; i++)
		{
			fwrite(&nodeCountPerDepth[i], sizeof(uint32_t), 1, file);
		}
	}
};

struct DagHeaderColorNode {
	const int version = 1;
	const int format = DagFormat::ColorPerNode;
	int maxDepth;
	uint32_t colorCount;

	std::vector<uint32_t> nodeCountPerDepth;
	std::vector<uint32_t> childrenCountPerDepth;

	DagHeaderColorNode() {}
	
	DagHeaderColorNode(SvoHeaderColorFull& header) : maxDepth(header.maxDepth), colorCount(header.GetNodeCount()), nodeCountPerDepth(header.maxDepth), childrenCountPerDepth(header.maxDepth) {}

	void Write(FILE* file)
	{
		fwrite(&version, sizeof(uint32_t), 1, file);
		fwrite(&format, sizeof(uint32_t), 1, file);

		fwrite(&maxDepth, sizeof(uint32_t), 1, file);

		auto nodeCount = std::accumulate(nodeCountPerDepth.begin(), nodeCountPerDepth.end(), 0);
		auto childrenCount = std::accumulate(childrenCountPerDepth.begin(), childrenCountPerDepth.end()-1, 0);

		fwrite(&nodeCount, sizeof(uint32_t), 1, file);
		fwrite(&childrenCount, sizeof(uint32_t), 1, file);
		fwrite(&childrenCountPerDepth[childrenCountPerDepth.size() - 1], sizeof(uint32_t), 1, file);
		fwrite(&colorCount, sizeof(uint32_t), 1, file);

		for (int i = 0; i < maxDepth; i++)
		{
			fwrite(&nodeCountPerDepth[i], sizeof(uint32_t), 1, file);
		}
	}
};