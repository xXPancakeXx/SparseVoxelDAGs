#pragma once
#include <cstdio>
#include <iostream>
#include <string>
#include <vector>
#include "Analysis.h"
#include "f.h"
#include "SvoHeaders.h"
#include "DagHeaders.h"
#include "LevelLoader.h"
#include "DagNodes.h"


class LevelLoaderGray : public LevelLoader<NodeGray, SvoHeaderGray, DagHeaderGray> {
public:
	LevelLoaderGray(const char* svoPath, const char* dagPath, const char* modelname, int depth = 999) : LevelLoader(svoPath, dagPath, modelname, depth)
	{

	}

protected:
	void LoadDepth(int depth, std::vector<NodeGray>& level) override
	{
		//Timer t("IO>LoadDepth", true);

		std::cout << "Loading " << depth << "" << std::endl;

		level.clear();

		auto nodesInDepth = svoHeader.nodesPerLevel[depth];
		bool hasErr = fseek(svoFile, (long)svoHeader.GetDepthOffsetByte(depth), SEEK_SET);
		if (hasErr) throw;

		level.resize(nodesInDepth);
		for (uint32_t i = 0; i < nodesInDepth; ++i) {
			NodeGray& node = level[i];

			uint64_t data;
			_fread_nolock(&data, sizeof(uint64_t), 1, svoFile);
			node.validMask = data >> 32;
			
			if (depth == dagHeader.maxDepth - 1) node.SetLeafNode();
			else node.SetTreeNode(data & 0xFF'FF'FF'FF);
		}
		std::cout << "Level " << depth << " loaded into memory" << std::endl;
	}
	
	void LoadDepthMapChildrenParents(int depth, std::vector<NodeGray>& parentLevel) override
	{
		//Timer t("IO>LoadDepthMap", true);
		
		parentLevel.clear();

		auto nodesInDepth = svoHeader.nodesPerLevel[depth];
		fseek(svoFile, svoHeader.GetDepthOffsetByte(depth), SEEK_SET);

		int childIdx = 0;
		parentLevel.reserve(nodesInDepth);
		for (int i = 0; i < nodesInDepth; ++i) {
			uint64_t data;
			_fread_nolock(&data, sizeof(uint64_t), 1, svoFile);

			NodeGray node(i, data >> 32 >> 8, childIdx, currentLevel);
			/*int childCount = node.CountChildren();
			for (size_t j = 0; j < childCount; j++)
			{
				currentLevel[childIdx].parent = i;
				currentLevel[childIdx++].parentChildId = j;
			}*/

			parentLevel.push_back(node);
		}

		std::cout << "Level " << depth << " loaded into memory" << std::endl;
	}
	
	void FinalizeDag() override
	{
		//Timer t("IO>Finalize", true);

		auto path = GetOutputFilePathName(dagPath, modelname, dagHeader.maxDepth, "Gray");
		FILE* dagFile = f::CreateOpenWrite(path.c_str());
		//Lifehack bc we sometimes get a nullptr, bc windows denies permission :(
		while (dagFile == nullptr) { dagFile = f::CreateOpenWrite(path.c_str()); std::cout << "Trying.." << std::endl; }

		dagHeader.Write(dagFile);
		currentDepth = 2;

		for (int i = 0; i < dagHeader.maxDepth; i++)
		{
			uint32_t nodesCurrentLevel = parentLevel.size();
			uint32_t childrenCurrentLevel = CountChildren(parentLevel);

			std::vector<int> accumulatedChildCountNextLevelBeforeIdx;
			AccumulateChildrenCount(currentLevel, accumulatedChildCountNextLevelBeforeIdx);

			uint32_t writtenNodesCurrentLevel = 0;
			uint32_t writtenChildrenCurrentLevel = 0;

			for (size_t j = 0; j < parentLevel.size(); j++)
			{
				int childCtr = 1;
				uint32_t dataToWrite[9];
				dataToWrite[0] = parentLevel[j].validMask;

				for (size_t k = 0; k < 8; k++)
				{
					if (parentLevel[j].HasChild(k))
					{
						uint32_t offset = 0;
						writtenChildrenCurrentLevel++;

						//before last level
						if (i < dagHeader.maxDepth - 1) {
							int indexNodeNextLevel = parentLevel[j].children[k];
							int childrenBeforeNodeNextLevel = accumulatedChildCountNextLevelBeforeIdx[indexNodeNextLevel];

							dataToWrite[childCtr++] =
								nodesCurrentLevel - writtenNodesCurrentLevel
								+ childrenCurrentLevel - writtenChildrenCurrentLevel
								+ childrenBeforeNodeNextLevel
								+ indexNodeNextLevel;
						}
						else {
							dataToWrite[childCtr++] = 0;
						}
					}
				}

				//_fwrite_nolock(&parentLevel[j].validMask, sizeof(int32_t), 1, dagFile);
				_fwrite_nolock(&dataToWrite, sizeof(uint32_t), childCtr, dagFile);

				writtenNodesCurrentLevel++;
			}

			parentLevel = currentLevel;
			if (i + 2 < dagHeader.maxDepth) ReadTempLevel(i + 2, currentLevel);
		}

		//Delete temp files
		for (size_t i = 0; i < levelTempPaths.size(); i++)
		{
			if (!levelTempPaths[i].empty())
				remove(levelTempPaths[i].c_str());
		}

		fclose(dagFile);
	}

private:
	uint32_t CountChildren(std::vector<NodeGray>& nodes)
	{
		uint32_t childCount = 0;
		for (size_t i = 0; i < nodes.size(); i++)
		{
			childCount += CountBits(nodes[i].validMask);
		}

		return childCount;
	}

	uint32_t AccumulateChildrenCount(std::vector<NodeGray>& nodes, std::vector<int>& accumulatedChildCountBeforeIdx)
	{
		uint32_t childCount = 0;
		for (size_t i = 0; i < nodes.size(); i++)
		{
			accumulatedChildCountBeforeIdx.push_back(childCount);
			childCount += CountBits(nodes[i].validMask);
		}

		return childCount;
	}
};