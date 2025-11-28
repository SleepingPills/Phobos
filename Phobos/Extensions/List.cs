using System;
using System.Collections.Generic;

namespace Phobos.Extensions;

public static class ListExtensions
{
    public static bool RemoveSwap<T>(this List<T> list, T member) where T : IEquatable<T>
    {
        for (var i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate.Equals(member)) continue;
            list.RemoveAtSwap(i);
            return true;
        }
        
        return false;
    }
    
    public static bool RemoveSwapBoxed<T>(this List<T> list, T member)
    {
        for (var i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate.Equals(member)) continue;
            list.RemoveAtSwap(i);
            return true;
        }
        
        return false;
    }
    
    public static void RemoveAtSwap<T>(this List<T> list, int index)
    {
        var lastIndex = list.Count - 1;
        list[index] = list[lastIndex];
        list.RemoveAt(lastIndex);
    }
}