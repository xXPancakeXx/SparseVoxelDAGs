#pragma once
#include <chrono>
#include <iostream>
#include <map>
#include <iomanip>

class MemoryTracker {
private:
	MemoryTracker() {}
public:
	inline static std::map<std::string, uint64_t> store;


	static void Snapshot(const std::string key, uint64_t value) {
		if (value > store[key])
			store[key] = value;
	}

	static void Print() {
		std::cout << "------------------------------------------------" << std::endl;
		for (auto it = store.begin(); it != store.end(); ++it) {
			std::cout << std::setw(20) << it->first << ": " << std::setw(10) << it->second << " B " << std::endl;
		}
	}

	static void Reset() 
	{
		store.clear();
	}
};

class GroupTimer {
public:
	struct Group {
		std::map<std::string, Group> subGroups;
		long long time;

		Group() : time(0) {}

		Group(long long time) : time(time)
		{

		}
	};

private:

	inline static Group baseGroup;

	GroupTimer() = delete;

public:
	static void AppendTime(const char* groupHierarchy, long long time) 
	{
		auto gr = std::string_view(groupHierarchy);
		auto* group = FindOrCreateGroup(&baseGroup, gr);

		group->time += time;

		return;
	}

	static void PrintTracked(bool includeSummary = true) 
	{
		std::cout << "Timers" << std::endl;
		std::cout << "------------------------------------------------" << std::endl;

		auto sum = PrintGroup(baseGroup);

		std::cout << "------------------------------------------------" << std::endl;
		std::cout << std::setw(20) << "Overall" << ": " << std::setw(10) << sum << " us " << std::setw(10) << (sum / 1000.f) << " ms" << std::endl;
	}

	static Group GetAndReset()
	{
		auto temp = baseGroup;
		baseGroup = Group();
		return temp;
	}

	static std::map<std::string, long long> Average(Group groups[], int groupCount) 
	{
		std::map<std::string, long long> resMap;
		for (size_t i = 0; i < groupCount; i++)
		{
			std::map<std::string, long long> map;
			Flatten(groups[i], map);

			for (auto it = map.begin(); it != map.end(); ++it)
			{
				resMap[it->first] += it->second;
			}
		}

		for (auto it = resMap.begin(); it != resMap.end(); ++it)
		{
			it->second /= groupCount;
		}

		return resMap;
	}

private:
	static Group* FindOrCreateGroup(Group* group, std::string_view& name) {
		auto pos = name.find('>');
		if (pos == std::string::npos) {
			auto grName = std::string(name.data(), name.size());
			auto& map = group->subGroups;
			return &map[grName];
		}

		auto groupName = name.substr(0, pos);
		auto nextGroupName = name.substr(pos + 1);

		auto grName = std::string(groupName.data(), groupName.size());
		auto* nextGroup = &group->subGroups[grName];

		return FindOrCreateGroup(nextGroup, nextGroupName);
	}

	static long long PrintGroup(const Group& group, int indent = 0) {
		long long totalTime = 0;
		for (auto it = group.subGroups.begin(); it != group.subGroups.end(); ++it) {
			auto& name = it->first;
			auto& nextGroup = it->second;
			
			std::string padding;
			for (size_t j = 0; j < indent; j++)
			{
				padding += " -> ";
			}

			auto thousands = std::make_unique<separate_thousands>();
			std::cout.imbue(std::locale(std::cout.getloc(), thousands.release()));

			std::cout <<
				std::left << std::setw(20) << (padding + std::string(name)) << ": " << 
				std::right << std::setw(10) << nextGroup.time << " us " << 
				std::setw(10) << std::fixed << std::setprecision(0) << (nextGroup.time / 1000.f) << " ms" <<
			std::endl;
			
			PrintGroup(nextGroup, indent + 1);
			totalTime += nextGroup.time;
		}

		return totalTime;
	}

	static void Flatten(const Group& group, std::map<std::string, long long>& outmap) {

		for (auto it = group.subGroups.begin(); it != group.subGroups.end(); ++it) {
			auto& name = it->first;
			auto& nextGroup = it->second;

			if (nextGroup.subGroups.size() == 0)
				outmap[name] = nextGroup.time;
			else
			{
				outmap[name] = nextGroup.time;
				Flatten(nextGroup, outmap);
			}
		}

	}

	struct separate_thousands : std::numpunct<char> {
		char_type do_thousands_sep() const override { return ' '; }  // separate with commas
		string_type do_grouping() const override { return "\3"; } // groups of 3 digit
	};
};

class Timer {
public:
	Timer(const char* text, bool useForGroup = false) : text(text), useForGroup(useForGroup) {
		startTime = std::chrono::high_resolution_clock::now();
	}

	Timer(const std::string& text, bool useForGroup = false) : text(text.c_str()), useForGroup(useForGroup) {
		startTime = std::chrono::high_resolution_clock::now();
	}

	Timer() : text(), useForGroup(false) {
		startTime = std::chrono::high_resolution_clock::now();
	}

	~Timer() {
		Stop();
	}

	void Stop() {
		auto endTime = std::chrono::high_resolution_clock::now();

		auto start = std::chrono::time_point_cast<std::chrono::microseconds>(startTime).time_since_epoch();
		auto end = std::chrono::time_point_cast<std::chrono::microseconds>(endTime).time_since_epoch();

		auto duration = end - start;
		auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(duration);

		if (useForGroup) {
			GroupTimer::AppendTime(text, duration.count());
		}
		else {
			std::cout << text << duration.count() << "us (" << ms.count() << "ms)" << std::endl;
		}
	}

private:
	std::chrono::time_point<std::chrono::high_resolution_clock> startTime;
	const char* text;
	bool useForGroup;
};