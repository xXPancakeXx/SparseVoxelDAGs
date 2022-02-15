#pragma once
#include <cstdio>
#include <iostream>
#include <string>
#include <vector>
#include "Analysis.h"
#include "f.h"
#include "SvoHeaders.h"
#include "DagHeaders.h"
#include "mio.h"

uint32_t CountBits(uint32_t i) {
	i = i - ((i >> 1) & 0x55555555);					// add pairs of bits
	i = (i & 0x33333333) + ((i >> 2) & 0x33333333);		// quads
	i = (i + (i >> 4)) & 0x0F0F0F0F;					// groups of 8
	return (i * 0x01010101) >> 24;
}

template <class T, class SvoHeader, class DagHeader>
class LevelLoader{
public:
	LevelLoader(const char* svoPath, const char* dagPath, const char* modelname, int depth = 999) 
		: svoHeader(svoPath), dagHeader(svoHeader), dagPath(dagPath), modelname(modelname)
	{
		svoFile = f::OpenRead(svoPath);

		//Set root child count and node count because they will never be iterated
		dagHeader.nodeCountPerDepth[0] = 1;
		dagHeader.childrenCountPerDepth[0] = svoHeader.nodesPerLevel[1];

		dagHeader.maxDepth = std::min(dagHeader.maxDepth, depth);
		currentDepth = dagHeader.maxDepth;
		levelTempPaths.resize(dagHeader.maxDepth);
	}

	~LevelLoader() {
		fclose(svoFile);
	}

	bool LoadNext()
	{
		Timer t("IO", true);

		if (currentDepth == 0) {
			FinalizeDag();
			return false;
		}
		if (currentDepth == dagHeader.maxDepth) {
			LoadLastLevel();
			currentDepth -= 2;
		}
		else {
			SaveTempCurrentLevel(currentDepth + 1, currentLevel);

			currentLevel = parentLevel;
			LoadDepthMapChildrenParents(--currentDepth, parentLevel);
		}
		return true;
	}

	std::uint64_t GetMemoryRequirement() {
		return sizeof(T) * (currentLevel.size() + parentLevel.size());
	}

private:
	void LoadLastLevel()
	{
		LoadDepth(dagHeader.maxDepth - 1, currentLevel);
		LoadDepthMapChildrenParents(dagHeader.maxDepth - 2, parentLevel);
	}

protected:
	virtual void LoadDepth(int depth, std::vector<T>& level) = 0;
	virtual void LoadDepthMapChildrenParents(int depth, std::vector<T>& parentLevel) = 0;
	virtual void FinalizeDag() = 0;

	
	void SaveTempCurrentLevel(int depth, std::vector<T>& nodes)
	{
		std::string path = "temp_" + std::to_string(depth);
		levelTempPaths[depth] = (path);

		auto* tmpFile = f::CreateOpenWrite(path.c_str());
		for (size_t i = 0; i < nodes.size(); i++)
		{
			_fwrite_nolock(&nodes[i], sizeof(T) - 8, 1, tmpFile);
		}

		fclose(tmpFile);
	}

	void ReadTempLevel(int depth, std::vector<T>& nodes)
	{
		nodes.clear();

		auto* tmpFile = f::OpenRead(levelTempPaths[depth].c_str());
		while (!feof(tmpFile))
		{
			auto& n = nodes.emplace_back();
			_fread_nolock(&n, sizeof(T) - 8, 1, tmpFile);
		}
		nodes.pop_back();

		fclose(tmpFile);
	}


	std::string GetOutputFilePathName(const char* dagPath, const char* modelName, int maxDepth, const char* format) {
		return std::string(dagPath) + std::string(modelName) + "_" + std::to_string(1 << maxDepth) + "_" + std::string(format) + ".dag";
	}


public:
	SvoHeader svoHeader;
	DagHeader dagHeader;

	std::vector<T> currentLevel;
	std::vector<T> parentLevel;

	int GetCurrentDepth() { return currentDepth + 1; }

protected:
	FILE* svoFile;

	const char* dagPath;
	const char* modelname;
	int currentDepth;

	std::vector<std::string> levelTempPaths;
};