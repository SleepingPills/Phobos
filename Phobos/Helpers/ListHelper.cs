using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Phobos.Helpers;

public static class ListHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SwapRemove<T>(this List<T> list, T member)
    {
        for (var i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (!candidate.Equals(member)) continue;
            list.SwapRemoveAt(i);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SwapRemoveAt<T>(this List<T> list, int index)
    {
        var lastIndex = list.Count - 1;
        list[index] = list[lastIndex];
        list.RemoveAt(lastIndex);
    }
}