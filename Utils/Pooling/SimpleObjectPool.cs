using System;
using System.Collections.Generic;

public class SimpleObjectPool<T> where T : class
{
    private Queue<T> availableItems = new Queue<T>();
    private object lockObject = new object();

    public T Get(/*params object[] args*/)
    {
        lock (lockObject)
        {
            if (availableItems.Count > 0)
            {
                T item = availableItems.Dequeue();
                return item;
            }
            else
            {
                return null;
                //T newItem = (T)Activator.CreateInstance(typeof(T), args);
                //return newItem;
            }
        }
    }

    public T GetOrCreate(params object[] args)
    {
        lock (lockObject)
        {
            if (availableItems.Count > 0)
            {
                T item = availableItems.Dequeue();
                return item;
            }
            else
            {
                T newItem = (T)Activator.CreateInstance(typeof(T), args);
                return newItem;
            }
        }
    }

    public void Return(T item)
    {
        lock (lockObject)
        {
            availableItems.Enqueue(item);
        }
    }

    public void Clear(Action<T> destroyAction = null)
    {
        lock (lockObject)
        {
            if (destroyAction != null)
            {
                foreach (T item in availableItems)
                {
                    destroyAction(item);
                }
            }

            availableItems.Clear();
        }
    }

    public static SimpleObjectPool<T> Shared { get; } = new SimpleObjectPool<T>();
}
