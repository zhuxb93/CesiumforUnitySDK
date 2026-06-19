using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace GeoTiles
{
    public class VectorObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;
        private readonly Transform _root;

        public VectorObjectPool(Func<T> objectGenerator, int initialCount, Transform root = null)
        {
            if (objectGenerator == null) throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
            _objectGenerator = objectGenerator;
            _root = root;
            for (int i = 0; i < initialCount; i++)
            {
                var obj = objectGenerator();
                if (obj is GameObject)
                {
                    var go = obj as GameObject;
                    //var mf = go.AddComponent<MeshFilter>();
                    //var mr = go.AddComponent<MeshRenderer>();
                    go.transform.SetParent(root);
                }
                _objects.Add(obj);
            }
        }

        public T GetObject()
        {
            T item;
            if (_objects.TryTake(out item)) return item;
            return _objectGenerator();
        }

        public void PutObject(T item)
        {
            _objects.Add(item);
        }
    }

}