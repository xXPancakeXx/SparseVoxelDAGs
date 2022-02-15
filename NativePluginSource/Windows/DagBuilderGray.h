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
#include "LevelLoaderGray.h"
#include "DagNodes.h"


class DagBuilderGray {
private:
	LevelLoaderGray loader;
public:
	DagBuilderGray(const char* svoPath, const char* dagPath, const char* modelname, int depth = 999) : loader(svoPath, dagPath, modelname, depth) {}

	void Convert() 
	{
		while(loader.LoadNext())
		{
			MemoryTracker::Snapshot("Node memory", loader.GetMemoryRequirement());
			Timer t("Algorithm", true);

			{
				Timer t("Algorithm>Sorting", true);
				std::sort(std::execution::par_unseq, loader.currentLevel.begin(), loader.currentLevel.end(), [](NodeGray& a, NodeGray& b) {
					return (a.validMask < b.validMask) || (a.validMask == b.validMask && memcmp(&a.children[0], &b.children[0], 8 * 4) < 0);
				});
			}

			{
				Timer t("Algorithm>Grouping", true);
				Group(loader.currentLevel, loader.parentLevel);
			}
		}

		GroupTimer::PrintTracked();
		MemoryTracker::Print();
	}

	uint32_t GetNodeCount() 
	{
		return std::accumulate(loader.dagHeader.nodeCountPerDepth.begin(), loader.dagHeader.nodeCountPerDepth.end(), 0);
	}

	uint32_t GetPointerCount()
	{
		return std::accumulate(loader.dagHeader.childrenCountPerDepth.begin(), loader.dagHeader.childrenCountPerDepth.end(), 0);
	}

private:
	void Group(std::vector<NodeGray>& nodes, std::vector<NodeGray>& parentNodes)
	{
		// Id of the node in this new list for comparison with old list
		auto groupNodeId = 0;
		//int newNodeId = 0;
		int childrenCount = 0;

		// Initialize new level
		std::vector<NodeGray> newLevel;
		newLevel.push_back(nodes[groupNodeId]);

		//Assert that the level is sorted
		for (int nId = 1; nId < nodes.size(); nId++)
		{
			// Old list
			if (newLevel[groupNodeId].Equals(nodes[nId]))
			{
				// Updating the children field of the parent node
				auto& parent = parentNodes[nodes[nId].parent];
				UpdateParentDirect(parent, nodes[nId].parentChildId, groupNodeId);
			}
			else
			{
				UpdateParentDirect(parentNodes[newLevel[groupNodeId].parent], newLevel[groupNodeId].parentChildId, groupNodeId);
				childrenCount += newLevel[groupNodeId].CountChildren();

				// New group of nodes
				newLevel.push_back(nodes[nId]);
				groupNodeId++;

			}
		}

		//Update last groups parent as well, since we are generating a new indexing scheme
		UpdateParentDirect(parentNodes[newLevel[groupNodeId].parent], newLevel[groupNodeId].parentChildId, groupNodeId);
		childrenCount += newLevel[groupNodeId].CountChildren();

		MemoryTracker::Snapshot("Algorithm memory", loader.GetMemoryRequirement() + (newLevel.size() * sizeof(NodeGray)));

		// Change the old level by the new level
		nodes = newLevel;

		loader.dagHeader.nodeCountPerDepth[loader.GetCurrentDepth()] = nodes.size();
		loader.dagHeader.childrenCountPerDepth[loader.GetCurrentDepth()] = childrenCount;

//#define DEBUG
#ifdef DEBUG
		AssertChildrenInRange(parentNodes, nodes);
#endif
	}

	inline void UpdateParent(NodeGray& parent, int oldId, int newId) 
	{
		for (int j = 0; j < 8; j++)
		{
			if (!parent.IsChildUpdated(j) && parent.children[j] == oldId) {
				parent.children[j] = newId;
				parent.updateMask |= 1 << j;
				return;
			}
		}
	}

	inline void UpdateParentDirect(NodeGray& parent, int parentChildId, int newId)
	{
		parent.children[parentChildId] = newId;
		parent.updateMask |= 1 << parentChildId;
	}

	uint32_t AssertChildrenInRange(std::vector<NodeGray>& parentLevel, std::vector<NodeGray>& childlevel) {
		uint32_t biggestIndex = 0;
		for (size_t i = 0; i < parentLevel.size(); i++)
		{
			for (size_t j = 0; j < 8; j++)
			{
				if (parentLevel[i].children[j] != -1) {
					assert(parentLevel[i].children[j] < childlevel.size());
					//std::cout << "i: " << i << " j: " << j << " value: " << parentLevel[i].children[j] << std::endl;
				}
			}
		}
		return biggestIndex;
	}
};
