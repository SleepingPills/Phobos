using Phobos.Data;
using Phobos.Entities;
using Phobos.Tasks.Actions.Controllers;

namespace Phobos.Tasks.Actions;

public abstract class BaseAction(AgentData dataset, float hysteresis) : Task<Agent>(hysteresis)
{
    protected readonly MovementController Mover = new();
    
    public override void Update()
    {
        Mover.Update(ActiveEntities);
    }

    public override void Activate(Agent entity)
    {
        Mover.Activate(entity);
        base.Activate(entity);
    }

    public override void Deactivate(Entity entity)
    {
        var agent = dataset.Entities[entity.Id];
        Mover.Deactivate(agent);
        base.Deactivate(entity);
    }
}