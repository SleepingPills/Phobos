using System.Runtime.CompilerServices;
using Phobos.Navigation;

namespace Phobos.Components;

public class Guard(int id) : Component(id)
{
    public Location Location;
    
    public override string ToString()
    {
        return $"{nameof(Guard)}({Location})";
    }
}