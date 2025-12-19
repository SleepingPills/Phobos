using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Components;

namespace Phobos.Data;

public interface IComponentArray
{
    public Component Add(int id);
    public bool Remove(int id);
}

public class ComponentArray<T>(Func<int, T> builderFunc, int capacity = 16) : IComponentArray where T : Component
{
    private readonly List<T> _data = new(capacity);

    public T this[int id]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _data[id];
    }

    public Component Add(int id)
    {
        var component = builderFunc(id);

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

        return component;
    }

    public bool Remove(int id)
    {
        var success = _data[id] != null;

        // If the id is the last entry, remove it altogether
        if (id == _data.Count - 1)
        {
            _data.RemoveAt(id);
        }
        else
        {
            _data[id] = null;
        }

        return success;
    }
}