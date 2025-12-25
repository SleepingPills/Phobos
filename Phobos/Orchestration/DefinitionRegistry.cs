using System;
using System.Collections.Generic;

namespace Phobos.Orchestration;

public class DefinitionRegistry<T>
{
    private readonly Dictionary<Type, T> _defs = new();
    
    public Dictionary<Type, T>.ValueCollection Values => _defs.Values;

    public void Add<TI>(TI instance) where TI: T
    {
        _defs.Add(typeof(TI), instance);
    }
    
    public void Remove<TI>() where TI: T
    {
        _defs.Remove(typeof(TI));
    }
}