namespace Phobos.ECS;

public interface IPhobosSystem
{
    public void AddActor(Actor actor);
    public void RemoveActor(Actor actor);
}