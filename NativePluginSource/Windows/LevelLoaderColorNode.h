#pragma once
#include <malloc.h>
#include <vector>
#include <iostream>
#include <string>
#include <bitset>
#include <execution>
#include <algorithm>
#include <cassert>
#include "Analysis.h"
#include "../Windows/f.h"
#include "SvoHeaders.h"
#include "DagHeaders.h"
#include "DagNodes.h"
#include <functional>
#include "mio.h"


class LevelLoaderColorNode : public LevelLoader<NodeColor, SvoHeaderColorFull, DagHeaderColorNode> {
private:
	const char* svoFilePath;
public:
	LevelLoaderColorNode(const char* svoFilePath, const char* dagPath, const char* modelname, int depth) : LevelLoader(svoFilePath, dagPath, modelname, depth), svoFilePath(svoFilePath)
	{
	}
	
	void LoadDepth(int depth, std::vector<NodeColor>& level) override
	{
		level.clear();

		auto nodesInDepth = svoHeader.nodesPerLevel[depth];
		bool hasErr = fseek(svoFile, (long)svoHeader.GetDepthOffsetByte(depth), SEEK_SET);
		if (hasErr) throw;

		level.resize(nodesInDepth);
		for (int i = 0; i < nodesInDepth; ++i) {
			NodeColor& node = level[i];

			uint64_t data;
			_fread_nolock(&data, sizeof(uint64_t), 1, svoFile);

			/*int validMaskColorId, childPtr;
			fread(&childPtr, sizeof(int32_t), 1, svoFile);
			fread(&validMaskColorId, sizeof(int32_t), 1, svoFile);*/
			//level.push_back(depth == svoHeader.maxDepth - 1 ? NodeColor(data >> 32) : NodeColor(data & 0xFF'FF'FF'FF, data >> 32));

			if (depth == dagHeader.maxDepth - 1) node.SetLeafNode(data >> 32);
			else node.SetTreeNode(data >> 32, data & 0xFF'FF'FF'FF, currentLevel);
		}
		std::cout << "Level " << depth << " loaded into memory" << std::endl;
	}

	void LoadDepthMapChildrenParents(int depth, std::vector<NodeColor>& parentLevel) override
	{
		parentLevel.clear();

		auto nodesInDepth = svoHeader.nodesPerLevel[depth];
		fseek(svoFile, svoHeader.GetDepthOffsetByte(depth), SEEK_SET);

		int childIdx = 0;
		parentLevel.reserve(nodesInDepth);
		for (int i = 0; i < nodesInDepth; ++i) {
			uint64_t data;
			_fread_nolock(&data, sizeof(uint64_t), 1, svoFile);

			//int validMaskColorId, childPtr;
			//fread(&childPtr, sizeof(int32_t), 1, svoFile);
			//fread(&validMaskColorId, sizeof(int32_t), 1, svoFile);

			parentLevel.push_back(NodeColor(i, data >> 32, childIdx, currentLevel));
		}

		std::cout << "Level " << depth << " loaded into memory" << std::endl;
	}



	void FinalizeDag() override
	{
		Timer t("IO>Finalize", true);

		auto path = GetOutputFilePathName(dagPath, modelname, dagHeader.maxDepth, "ColorPerNode");
		FILE* dagFile = f::CreateOpenWrite(path.c_str());
		
		dagHeader.Write(dagFile);
		currentDepth = 2;

		for (int i = 0; i < dagHeader.maxDepth; i++)
		{
			if (i < dagHeader.maxDepth - 2) {
				WriteLevel(dagFile, parentLevel, currentLevel, i, 2, 2, 1);
			}
			//Write penultimate level in respect to last level (no childpointers and headers are of size 1)
			else if (i == dagHeader.maxDepth - 2) {
				WriteLevel(dagFile, parentLevel, currentLevel, i, 2, 1, 0);
			}
			else if (i == dagHeader.maxDepth - 1) {
				//write leaf level
				for (size_t j = 0; j < parentLevel.size(); j++)
				{
					int vm = parentLevel[j].GetValidMask();
					fwrite(&vm, sizeof(int32_t), 1, dagFile);
				}
			}

			parentLevel = currentLevel;
			if (i + 2 < dagHeader.maxDepth) ReadTempLevel(i + 2, currentLevel);
		}
		

		std::cout << "Converting colors..." << std::endl;
		{
			Timer t("IO>Finalize>Colors", true);

			mio::basic_mmap_source<uint8_t> mmapFile(svoFilePath, 0, mio::map_entire_file);
			IterateDepthFirst(mmapFile, dagFile, 0, 0);
		}


		std::cout << "Deleting temp files..." << std::endl;
		for (size_t i = 0; i < levelTempPaths.size(); i++)
		{
			if (!levelTempPaths[i].empty())
				remove(levelTempPaths[i].c_str());
		}

		fclose(dagFile);
	}

private:

	void WriteLevel(FILE* dagFile, std::vector<NodeColor> nodes, std::vector<NodeColor> nextLvlNodes, int level, int currLevelHeaderMultiplier, int nextLevelHeaderMultiplier, int nextLevelChildrenMultiplier)
	{
		int currLvlNodeNum = 0;
		int currLvlChildNum = 0;
		int currLvlNodeCount = nodes.size() * currLevelHeaderMultiplier;
		int currLvlChildCount = dagHeader.childrenCountPerDepth[level];

		uint32_t childrenCurrentLevel = CountChildren(nextLvlNodes);
		std::vector<int> accumulatedChildCountNextLevelBeforeIdx;
		AccumulateChildrenCount(nextLvlNodes, accumulatedChildCountNextLevelBeforeIdx);

		for (int i = 0; i < nodes.size(); i++)
		{
			auto& node = nodes[i];

			int validMask = node.GetValidMask();
			_fwrite_nolock(&validMask, sizeof(uint32_t), 1, dagFile);
			_fwrite_nolock(&node.subtreeCount, sizeof(uint32_t), 1, dagFile);

			for (int j = 0; j < 8; j++)
			{
				if (!(validMask & (1 << j))) continue;

				currLvlChildNum++;
				
				int indexNodeNextLevel = nodes[i].children[j];
				int childrenBeforeNodeNextLevel = accumulatedChildCountNextLevelBeforeIdx[indexNodeNextLevel];

				uint32_t offset;
				offset = currLvlNodeCount - currLvlNodeNum
					   + currLvlChildCount - currLvlChildNum
					   + indexNodeNextLevel * nextLevelHeaderMultiplier 
					   + childrenBeforeNodeNextLevel * nextLevelChildrenMultiplier 
					   - 1;

				_fwrite_nolock(&offset, sizeof(uint32_t), 1, dagFile);
			}

			currLvlNodeNum += currLevelHeaderMultiplier;
		}
	}

	uint32_t CountChildren(std::vector<NodeColor>& nodes)
	{
		uint32_t childCount = 0;
		for (size_t i = 0; i < nodes.size(); i++)
		{
			childCount += CountBits(nodes[i].GetValidMask());
		}

		return childCount;
	}

	uint32_t AccumulateChildrenCount(std::vector<NodeColor>& nodes, std::vector<int>& accumulatedChildCountBeforeIdx)
	{
		uint32_t childCount = 0;
		for (size_t i = 0; i < nodes.size(); i++)
		{
			accumulatedChildCountBeforeIdx.push_back(childCount);
			childCount += CountBits(nodes[i].GetValidMask());
		}

		return childCount;
	}

	void IterateDepthFirst(mio::basic_mmap_source<uint8_t>& svoMap, FILE* dagFile, const uint32_t& nodeId, const int& depth/*, std::function<void(const uint32_t& validMaskColorId, const uint32_t& nodeId)> func*/)
	{
		uint32_t childPtr = svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u]
			| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 1u] << 8
			| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 2u] << 16
			| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 3u] << 24;

		uint8_t validMask = svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 4u];
		uint32_t colId = svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 5u]
			| svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 6u] << 8
			| svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 7u] << 16;

		//std::cout << "Depth " << depth << ":" << (int) validMask << " " << childPtr << std::endl;

		uint32_t color;
		ReadColorIndex(svoMap, colId, color);
		_fwrite_nolock(&color, sizeof(uint32_t), 1, dagFile);

		if (depth == svoHeader.maxDepth)
			return;

		for (int i = 0; i < 8; i++)
		{
			if (!(validMask & (1 << i))) continue;

			int x = (1 << i) - 1;
			int off = CountBits(validMask & x);

			long childIndex = nodeId + childPtr + off;
			IterateDepthFirst(svoMap, dagFile, childIndex, depth + 1);
		}
	}

	void IterateDepthFirst2(mio::basic_mmap_source<uint8_t>& svoMap, FILE* dagFile, uint32_t nodeId, int depth/*, std::function<void(const uint32_t& validMaskColorId, const uint32_t& nodeId)> func*/)
	{
		uint32_t childPtr = svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u]
			| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 1u] << 8
			| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 2u] << 16
			| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 3u] << 24;

		uint8_t validMask = svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 4u];
		uint32_t colId = svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 5u]
			| svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 6u] << 8
			| svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 7u] << 16;

		uint32_t color;
		ReadColorIndex(svoMap, colId, color);
		_fwrite_nolock(&color, sizeof(uint32_t), 1, dagFile);
		
		//std::cout << "Depth " << depth << ":" << std::fixed << std::setprecision(3) << (int)validMask << " " << childPtr << std::endl;

		int i = 0;
		std::tuple<int, int, uint8_t, uint8_t> iterateStack[23];
		x: while (depth >= 0)
		{
			for (; i < 8 && depth != svoHeader.maxDepth; i++)
			{
				if (!(validMask & (1 << i))) continue;

				int x = (1 << i) - 1;
				int off = CountBits(validMask & x);

				long childIndex = nodeId + childPtr + off;

				iterateStack[depth++] = std::make_tuple(nodeId, childPtr, i + 1, validMask);
				nodeId = childIndex;
				i = 0;

				childPtr = svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u]
					| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 1u] << 8
					| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 2u] << 16
					| svoMap[svoHeader.GetHeaderByteSize() + (nodeId) * 8u + 3u] << 24;

				validMask = svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 4u];
				colId = svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 5u]
					| svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 6u] << 8
					| svoMap[svoHeader.GetHeaderByteSize() + nodeId * 8u + 7u] << 16;

				ReadColorIndex(svoMap, colId, color);
				_fwrite_nolock(&color, sizeof(uint32_t), 1, dagFile);

				//std::cout << "Depth " << depth << ":" << std::fixed << std::setprecision(3) << (int)validMask << " " << childPtr << std::endl;

				goto x;
			}

			std::tie(nodeId, childPtr, i, validMask) = iterateStack[--depth];
		}

	}

	inline void ReadColorIndex(mio::basic_mmap_source<uint8_t>& svoMap, const uint32_t& colorIdx, uint32_t& color)
	{
		uint32_t idx = colorIdx * 4u + svoHeader.GetColorOffsetByte();
		color = svoMap[idx] | svoMap[idx + 1] << 8 | svoMap[idx + 2] << 16 | svoMap[idx + 3] << 24;
	}
};