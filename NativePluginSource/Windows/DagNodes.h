#pragma once
#include <cstdint>

struct NodeColor {
private:
	int validMaskColorId;

public:
	int subtreeCount;
	int children[8];

	//only used during processing
	uint8_t updateMask;
	uint8_t parentChildId;
	int parent;

	NodeColor() : updateMask(0), parent(0), validMaskColorId(0), subtreeCount(0) {}

	NodeColor(const int parentIdx, const int validMaskColorId, int& childrenIdx, std::vector<NodeColor>& children)
	{
		this->validMaskColorId = validMaskColorId;
		this->parent = 0;
		this->parentChildId = 0;
		this->updateMask = 0;
		this->subtreeCount = 1;

		for (size_t i = 0; i < 8; i++)
		{
			if (HasChild(i))
			{
				subtreeCount += children[childrenIdx].subtreeCount;

				children[childrenIdx].parent = parentIdx;
				children[childrenIdx].parentChildId = i;

				this->children[i] = childrenIdx++;
			}
			else
			{
				this->children[i] = -1;
			}
		}
	}

	//use this for leaf level nodes
	NodeColor(const int& validMaskColorId)
	{
		this->validMaskColorId = validMaskColorId;
		this->parent = 0;
		this->updateMask = 0;
		this->parentChildId = 0;
		this->subtreeCount = CountBits(GetValidMask()) + 1;

		memset(&children, -1, 4 * 8);
	}

	inline void SetLeafNode(const int& validMaskColorId)
	{
		this->validMaskColorId = validMaskColorId; 
		this->subtreeCount = CountBits(GetValidMask()) + 1;

		memset(&children, -1, 4 * 8);
	}

	inline void SetTreeNode(const int& validMaskColorId, const int& childrenIdx, std::vector<NodeColor>& children)
	{
		this->validMaskColorId = validMaskColorId;
		this->subtreeCount = 1;

		int childCounter = 0;
		for (size_t i = 0; i < 8; i++)
		{
			if (HasChild(i))
			{
				subtreeCount += children[childrenIdx].subtreeCount;

				this->children[i] = childrenIdx + childCounter++;
			}
			else
			{
				this->children[i] = -1;
			}
		}
	}

	inline int GetValidMask()
	{
		return validMaskColorId & 0xFF;
	}

	inline int GetColorId()
	{
		return validMaskColorId >> 8;
	};

	inline bool HasChild(int childBit) {
		return GetValidMask() & (1 << childBit);
	}

	inline int CountChildren() {
		return CountBits(GetValidMask());
	}

	inline bool IsChildUpdated(int childBit) {
		return updateMask & (1 << childBit);
	}

	bool Equals(NodeColor& other)
	{
		if (other.GetValidMask() != GetValidMask()) return false;
		for (int j = 0; j < 8; j++)
		{
			if (other.children[j] != children[j])
				return false;
		}
		return true;
	}
};



struct NodeGray {
	int validMask;
	int children[8];

	//only used during processing
	uint8_t updateMask;
	uint8_t parentChildId;
	int parent;

	NodeGray() : updateMask(0), parent(0), validMask(0), parentChildId(0) {}

	//use this for non leaf nodes
	//also sets the parent of the child correctly
	//beware that childrenIdx and children will be updated
	NodeGray(const int parentIdx, const int validmask, int& childrenIdx, std::vector<NodeGray>& children)
	{
		this->validMask = validmask;
		this->parent = 0;
		this->parentChildId = 0;
		this->updateMask = 0;

		for (size_t i = 0; i < 8; i++)
		{
			if (HasChild(i))
			{
				children[childrenIdx].parent = parentIdx;
				children[childrenIdx].parentChildId = i;

				this->children[i] = childrenIdx++;
			}
			else
			{
				this->children[i] = -1;
			}
		}
	}

	//use this for leaf level nodes
	NodeGray(const int& validmask)
	{
		this->validMask = validmask;
		this->parent = 0;
		this->parentChildId = 0;
		this->updateMask = 0;

		memset(&children, -1, 4 * 8);
	}

	inline void SetLeafNode() 
	{
		validMask >>= 8;
		memset(&children, -1, 4 * 8);
	}

	inline void SetTreeNode(const int& childrenIdx)
	{
		validMask >>= 8;
		int childCounter = 0;
		for (size_t i = 0; i < 8; i++)
		{
			if (HasChild(i))
			{
				this->children[i] = childrenIdx + childCounter++;
			}
			else
			{
				this->children[i] = -1;
			}
		}
	}

	bool HasChild(int childBit) {
		return validMask & (1 << childBit);
	}

	int CountChildren() {
		return CountBits(validMask);
	}

	bool IsChildUpdated(int childBit) {
		return updateMask & (1 << childBit);
	}

	bool Equals(NodeGray& other)
	{
		if (other.validMask != validMask) return false;
		return memcmp(other.children, children, 8 * sizeof(int)) == 0;

		//return other.validMask == validMask && memcmp(other.children, children, 8 * sizeof(int)) == 0;
		/*for (int j = 0; j < 8; j++)
		{
			if (other.children[j] != children[j])
				return false;
		}
		return true;*/
	}
};