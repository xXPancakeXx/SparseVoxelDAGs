#pragma once
#include <cstdio>
#include <iostream>
#include <string.h>

namespace f {
	FILE* OpenRead(const char* path) {
		FILE* file = nullptr;
		errno_t err;

		if ((err = fopen_s(&file, path, "rb")) != 0) {
			//perror("Cannot open file '%s'\n");

			fprintf(stderr, "cannot open file '%s': %s\n", path, strerror(err));

			/*char buf[strerrorlen_s(err) + 1];
			strerror_s(buf, sizeof buf, err);
			fprintf_s(stderr, "cannot open file '%s': %s\n", path, buf);*/
		}
		else {
			std::cout << "File opened: " << path << std::endl;
		}

		return file;
	}

	FILE* CreateOpenWrite(const char* path) {
		FILE* file = nullptr;
		errno_t err;

		remove(path);
		if ((err = fopen_s(&file, path, "wb")) != 0) {
			fprintf(stderr, "cannot open file '%s': %s\n", path, strerror(err));
		}
		else {
			std::cout << "File opened: " << path << std::endl;
		}
		return file;
	}
}

