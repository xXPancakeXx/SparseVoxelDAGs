#define NOMINMAX

#include "../Windows/DagBuilderGray.h"
#include "../Windows/DagBuilderColorNode.h"
#include <errno.h>
#include <string.h>
#include <fstream>
#include <stdio.h>
#include <iostream>

#include "../hlslpp-3.1/include/hlsl++.h"


using namespace hlslpp;


void LogConversionTime()
{
	const char* logName = "C:\\Users\\phste\\Desktop\\svo_svdag.log";
	const size_t sampleCount = 3;

	const char* path = "D:\\Bsc\\SparseVoxelDAGs\\Assets\\Data\\";

	const char* models[] = {
		"lucy"		   ,
		"fish"		   ,
		"sanmiguel"    ,
		"city"		   ,
		"bunny"		   ,
		"powerplant"   ,
		"hairball"
	};

	const int res[] = {
		8192,
		8192,
		8192,
		2048,
		8192,
		8192,
		4096
	};

	const int testModels[] = {
		2
	};

	const char separator = '&';
	for (size_t x = 0; x < 7; x++)
	{
		int i = x;

		for (size_t k = res[i]; k >= 1024; k >>= 1)
		{
			MemoryTracker::Reset();
			GroupTimer::GetAndReset();

			GroupTimer::Group samples[sampleCount];
			for (size_t j = 0; j < sampleCount; j++)
			{
				std::string p(std::string(path) + std::string(models[i]) + "_" + std::to_string(res[i]) + ".oct");
				DagBuilderGray builder(p.c_str(), "", "model", std::log2(k));
				builder.Convert();

				samples[j] = GroupTimer::GetAndReset();
			}
			_fcloseall();
			auto timeMap = GroupTimer::Average(samples, sampleCount);

			//header
			std::ofstream file(logName, std::ios_base::app);
			if (i == 0 && k == res[i]) {
				file << "Model" << separator << "Resolution" << separator << "MaxMemory" << separator << "Total";
				for (auto it = timeMap.begin(); it != timeMap.end(); ++it)
				{
					file << separator << it->first;
				}
				file << std::endl;
			}

			file << models[i] << separator << k << separator
				<< MemoryTracker::store["Node memory"] << separator
				<< std::fixed << std::setprecision(0) << (timeMap["IO"] + timeMap["Algorithm"]) / 1000.0f;
			for (auto it = timeMap.begin(); it != timeMap.end(); ++it)
			{
				file << separator << std::fixed << std::setprecision(0) << (it->second / 1000.0f);
			}
			file << std::endl;
			file.close();
		}
	}
}

void LogNodeChildrenCount()
{
	const char* logName = "C:\\Users\\phste\\Desktop\\svdag_children.log";
	const size_t sampleCount = 3;

	const char* path = "D:\\Bsc\\SparseVoxelDAGs\\Assets\\Data\\";

	const int modelCount = 7;
	const std::tuple<std::string, int, int> models[modelCount] = {
		std::tuple<std::string, int, int>("lucy"			,8192 * 2		,8192 * 2),
		std::tuple<std::string, int, int>("powerplant"		,8192		,8192),
		std::tuple<std::string, int, int>("fish"			,8192 * 4	,8192 * 4),
		std::tuple<std::string, int, int>("bunny"			,8192 * 2	,8192 * 2),
		std::tuple<std::string, int, int>("hairball"		,4096		,4096),
		std::tuple<std::string, int, int>("sanmiguel"		,8192		,8192),
		std::tuple<std::string, int, int>("city"			,2048		,2048),
	};

	const char separator = '&';
	std::ofstream file(logName, std::ios_base::app);
	file << "Model";
	for (size_t k = 512; k <= 8192 * 4; k <<= 1)
		file << separator << k;
	file << std::endl;


	for (const auto [name, maxRes, usedRes] : models)
	{
		const int minDim = 512;

		int count = log2(usedRes) - log2(512) + 1;
		uint32_t* nodeCount = new uint32_t[count];
		uint32_t* childCount = new uint32_t[count];
		int c = 0;
		for (size_t k = minDim; k <= usedRes; k <<= 1)
		{
			std::string p(std::string(path) + std::string(name) + "_" + std::to_string(usedRes) + ".oct");
			DagBuilderGray builder(p.c_str(), "", "model", std::log2(k));
			builder.Convert();

			//model name
			if (k == 512) {
				file << name;
			}

			nodeCount[c] = builder.GetNodeCount();
			childCount[c++] = builder.GetPointerCount();
		}

		for (size_t j = 0; j < c; j++)
		{
			file << separator << nodeCount[j];
		}
		file << std::endl;
		for (size_t j = 0; j < c; j++)
		{
			file << separator << childCount[j];
		}
		file << std::endl;

		delete[] nodeCount;
		delete[] childCount;
	}

	file.close();
}

template <size_t T>
void ConvertAverage(const char* path, int dim) {
	GroupTimer::Group samples[T];
	for (size_t j = 0; j < T; j++)
	{
		DagBuilderGray builder(path, "", "model", std::log2(dim));
		builder.Convert();

		samples[j] = GroupTimer::GetAndReset();
	}

	auto timeMap = GroupTimer::Average(samples, T);

	auto& file = std::cout;
	const char separator = ';';
	file << "Model" << separator << "Resolution" << separator << "MaxMemory";
	for (auto it = timeMap.begin(); it != timeMap.end(); ++it)
	{
		file << separator << it->first;
	}
	file << std::endl;

	file << path << separator << dim << separator << MemoryTracker::store["Node memory"];
	for (auto it = timeMap.begin(); it != timeMap.end(); ++it)
	{
		file << separator << std::fixed << std::setprecision(0) << (it->second / 1000.0f);
	}
	file << std::endl;

	MemoryTracker::Reset();
}

int main()
{
	//const char* path = "D:\\Bsc\\SparseVoxelDAGs\\Assets\\Data\\sanmiguel_4096_col.oct";
	//DagBuilderColorNode builder(path, "", "model");
	//builder.Convert();

	const char* path = "D:\\Bsc\\SparseVoxelDAGs\\Assets\\Data\\hairball_4096.oct";
	DagBuilderGray builder(path, "", "model");
	builder.Convert();

	//std::cout << builder.GetNodeCount()<<	std::endl;
	//std::cout << builder.GetPointerCount() <<	std::endl;

	//ConvertAverage<1>("D:\\Bsc\\SparseVoxelDAGs\\Assets\\Data\\powerplant_8192.oct", 8192);

	//LogConversionTime();
	//LogNodeChildrenCount();

	float3 x(3);
	std::cout << x.x;
}
