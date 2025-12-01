using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.Navigation;

public class NavJob(Vector3 origin, Vector3 destination)
{
    public readonly Vector3 Origin = origin;
    public readonly Vector3 Destination = destination;
    public NavMeshPathStatus Status = NavMeshPathStatus.PathInvalid;
    public Vector3[] Path;
    public bool IsReady => Path != null;
}

public class NavJobExecutor(int batchSize = 5)
{
    private readonly Queue<NavJob> _jobQueue = new(20);

    public NavJob Submit(Vector3 origin, Vector3 target)
    {
        var job = new NavJob(origin, target);
        Submit(job);
        return job;
    }

    public void Submit(NavJob job)
    {
        _jobQueue.Enqueue(job);
    }

    public void Update()
    {
        // We ramp the batch size - if we only have a few items in the queue, spread them out over more frames.
        // If the queue size reaches 2x the batch size, we run full batches.
        var counter = 0;
        var rampedBatchSize = Mathf.Min(Mathf.CeilToInt(_jobQueue.Count / 2f), batchSize);
        
        while (_jobQueue.Count > 0 && counter < rampedBatchSize)
        {
            var job = _jobQueue.Dequeue();
            var path = new NavMeshPath();
            NavMesh.CalculatePath(job.Origin, job.Destination, NavMesh.AllAreas, path);
            job.Path = path.corners;
            job.Status = path.status;
            counter++;
        }
    }
}