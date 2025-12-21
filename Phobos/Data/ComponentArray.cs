using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Phobos.Data;

public interface IComponentArray
{
    public void Add(int id);
    public void Remove(int id);
}

public class ComponentArray<T>(int capacity = 16) : IComponentArray where T : class, new()
{
    private readonly List<T> _data = new(capacity);

    public T this[int id]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _data[id];
    }

    public void Add(int id)
    {
        T component = new();

        if (id >= _data.Count)
        {
            // We are adding a new slot instead of reusing an old one.
            // Add empty slots up to and excluding the one represented by the id.
            for (var i = _data.Count; i < id; i++)
            {
                _data.Add(null);
            }

            _data.Add(component);
        }
        else
        {
            _data[id] = component;
        }
    }

    public void Remove(int id)
    {
        // If the id is the last entry, remove it altogether
        if (id == _data.Count - 1)
        {
            _data.RemoveAt(id);
        }
        else
        {
            _data[id] = null;
        }
    }
}