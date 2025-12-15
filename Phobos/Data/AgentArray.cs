using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Entities;

namespace Phobos.Data;

public class AgentArray(int capacity = 8)
{
    public readonly List<Agent> Values = new(capacity);

    private readonly List<int?> _idSlots = new(capacity);
    private readonly Stack<int> _freeIds = new(capacity);

    public Agent this[int id]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var index = _idSlots[id];
            return index != null ? Values[index.Value] : throw new KeyNotFoundException($"Key {id} not found");
        }
    }

    public Agent Add(BotOwner bot)
    {
        var valueIndex = Values.Count;

        int id;

        if (_freeIds.Count > 0)
        {
            // Pop the next free id
            id = _freeIds.Pop();

            // Map it to the index of the new value
            _idSlots[id] = valueIndex;
        }
        else
        {
            // A new entry has to be added
            id = valueIndex;
            _idSlots.Add(id);
        }

        var agent = new Agent(bot, id);
        Values.Add(agent);
        return agent;
    }

    public bool Remove(Agent agent)
    {
        var slot = _idSlots[agent.Id];

        if (!slot.HasValue)
            return false;

        var agentValueIndex = slot.Value;
        var lastValueIndex = Values.Count - 1;

        // Swap the last item into the location of the item being removed
        Values[agentValueIndex] = Values[lastValueIndex];
        Values.RemoveAt(lastValueIndex);

        // If we are now empty, bail out
        if (Values.Count == 0)
        {
            _freeIds.Clear();
            _idSlots.Clear();
            return true;
        }

        if (agent.Id == _idSlots.Count - 1)
        {
            // If agent.Id is the last entry in the idToIndexMap, pop that entry so we can shrink the array.
            _idSlots.RemoveAt(agent.Id);
        }
        else
        {
            // Otherwise add the id to the free id queue
            _freeIds.Push(agent.Id);
            _idSlots[agent.Id] = null;
        }

        // Nothing to do if we removed the last value in the array
        if (agentValueIndex == lastValueIndex) return true;

        // Update id -> index map for the value that was swapped around
        var swapped = Values[agentValueIndex];
        _idSlots[swapped.Id] = agentValueIndex;

        return true;
    }
}