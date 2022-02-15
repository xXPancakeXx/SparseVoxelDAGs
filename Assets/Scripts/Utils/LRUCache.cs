using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Utils
{
    public class LRUCache<K, V>
    {
        private readonly int capacity;
        private readonly Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>> cacheMap;
        private readonly LinkedList<LRUCacheItem<K, V>> lruList;

        public LRUCache(int capacity)
        {
            this.capacity = capacity;
            this.lruList = new LinkedList<LRUCacheItem<K, V>>();
            this.cacheMap = new Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>>(capacity);
        }

        public V Get(K key)
        {
            LinkedListNode<LRUCacheItem<K, V>> node;
            if (cacheMap.TryGetValue(key, out node))
            {
                V value = node.Value.value;
                lruList.Remove(node);
                lruList.AddLast(node);
                return value;
            }
            return default(V);
        }

        public bool TryGet(K key, out V value)
        {
            LinkedListNode<LRUCacheItem<K, V>> node;
            if (cacheMap.TryGetValue(key, out node))
            {
                value = node.Value.value;
                lruList.Remove(node);
                lruList.AddLast(node);

                return true;
            }

            value = default(V);
            return false;
        }

        public void Add(K key, V val)
        {
            if (cacheMap.TryGetValue(key, out var existingNode))
            {
                lruList.Remove(existingNode);
            }
            else if (cacheMap.Count >= capacity)
            {
                RemoveFirst();
            }

            LRUCacheItem<K, V> cacheItem = new LRUCacheItem<K, V>(key, val);
            LinkedListNode<LRUCacheItem<K, V>> node = new LinkedListNode<LRUCacheItem<K, V>>(cacheItem);
            lruList.AddLast(node);

            if (cacheMap.ContainsKey(key)) cacheMap.Add(key, node);
            else cacheMap[key] = node;
        }

        private void RemoveFirst()
        {
            // Remove from LRUPriority
            LinkedListNode<LRUCacheItem<K, V>> node = lruList.First;
            lruList.RemoveFirst();

            // Remove from cache
            cacheMap.Remove(node.Value.key);
        }
    }

    class LRUCacheItem<K, V>
    {
        public LRUCacheItem(K k, V v)
        {
            key = k;
            value = v;
        }

        public K key;
        public V value;
    }
}