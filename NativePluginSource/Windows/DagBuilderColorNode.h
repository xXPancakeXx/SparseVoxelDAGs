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
#include "LevelLoaderColorNode.h"


class DagBuilderColorNode {
private:
	LevelLoaderColorNode loader;
public:
	DagBuilderColorNode(const char* svoFilePath, const char* dagPath, const char* modelname, int depth = 999) : loader(svoFilePath, dagPath, modelname, depth) {}

	void Convert() 
	{
		while (loader.LoadNext())
		{
			MemoryTracker::Snapshot("Node memory", loader.GetMemoryRequirement());
			Timer t("Algorithm", true);

			/*std::vector<NodeValidMask> sortedNodes;
			sortedNodes.reserve(loader.currentLevel.size());
			for (size_t i = 0; i < loader.currentLevel.size(); i++)
			{
				NodeValidMask& n = sortedNodes.emplace_back();

				n.nodeId = i;
				n.validMask = loader.currentLevel[i].GetValidMask();
			}*/

			{
				Timer t("Algorithm>Sorting", true);
				std::sort(std::execution::par_unseq, loader.currentLevel.begin(), loader.currentLevel.end(), [](NodeColor& a, NodeColor& b) {
					//return a.validMask < b.validMask;
					return (a.GetValidMask() < b.GetValidMask()) || (a.GetValidMask() == b.GetValidMask() && memcmp(&a.children[0], &b.children[0], 8 * 4) < 0);
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

private:
//	//void Group(std::vector<NodeValidMask>& sortedNodes, std::vector<NodeColor>& nodes, std::vector<NodeColor>& parentNodes)
//	{
//		// Id of the node in this new list for comparison with old list
//		auto groupNodeId = sortedNodes[0].nodeId;
//		int newNodeId = 0;
//		int childrenCount = 0;
//
//		// Initialize new level
//		std::vector<NodeColor> newLevel;
//		newLevel.push_back(nodes[groupNodeId]);
//		UpdateParent(parentNodes[nodes[groupNodeId].parent], groupNodeId, newNodeId);
//		childrenCount += nodes[groupNodeId].CountChildren();
//
//		//Assert that the level is sorted
//		for (int i = 1; i < sortedNodes.size(); i++)
//		{
//			// Old list
//			auto nId = sortedNodes[i].nodeId;
//			if (nodes[groupNodeId].Equals(nodes[nId]))
//			{
//				// Updating the children field of the parent node
//				UpdateParent(parentNodes[nodes[nId].parent], nId, newNodeId);
//			}
//			else
//			{
//				UpdateParent(parentNodes[nodes[groupNodeId].parent], groupNodeId, newNodeId);
//
//				// New group of nodes
//				newLevel.push_back(nodes[nId]);
//				newNodeId++;
//				groupNodeId = nId;
//
//				childrenCount += nodes[nId].CountChildren();
//			}
//		}
//
//		//Update last groups parent as well, since we are generating a new indexing scheme
//		UpdateParent(parentNodes[nodes[groupNodeId].parent], groupNodeId, newNodeId);
//
//		// Change the old level by the new level
//		nodes = newLevel;
//
//		loader.dagHeader.nodeCountPerDepth[loader.GetCurrentDepth()] = nodes.size();
//		loader.dagHeader.childrenCountPerDepth[loader.GetCurrentDepth()] = childrenCount;
//
//#if DEBUG
//		uint32_t dagChildren = AssertChildrenInRange(parentNodes, nodes);
//#endif
//	}

	void Group(std::vector<NodeColor>& nodes, std::vector<NodeColor>& parentNodes)
	{
		// Id of the node in this new list for comparison with old list
		auto groupNodeId = 0;
		//int newNodeId = 0;
		int childrenCount = 0;

		// Initialize new level
		std::vector<NodeColor> newLevel;
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

	inline void UpdateParent(NodeColor& parent, int oldId, int newId)
	{
		for (int j = 0; j < 8; j++)
		{
			if (!parent.IsChildUpdated(j) && parent.children[j] == oldId) {
				parent.children[j] = newId;
				parent.updateMask |= 1 << j;
			}
		}
	}

	inline void UpdateParentDirect(NodeColor& parent, int parentChildId, int newId)
	{
		parent.children[parentChildId] = newId;
		parent.updateMask |= 1 << parentChildId;
	}

	uint32_t AssertChildrenInRange(std::vector<NodeColor>& parentLevel, std::vector<NodeColor>& childlevel) {
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
