using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Timing;

namespace Content.Server.Procedural;

public sealed partial class ProceduralSystem
{
    private sealed class LoadChunkJob : Job<bool>
    {
        public ProceduralSystem System;

        public ProceduralMetaLayer Meta;

        public LoadChunkJob(double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
        }

        public LoadChunkJob(double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
        }

        protected override Task<bool> Process()
        {
            // TODO: Put "Dungeon" on DungeonData and use that I guess?
            // Then like uhh corridor gen just goes "hey dungeondata gib corridors".
            // Have each meta layer has its own data BUT if it depends upon another layer it gets combined.

            foreach (var layer in Meta.Layers)
            {
                // TODO: Move all the dungeon shit here as this is pretty much dungeon loading now.
            }



            throw new NotImplementedException();
        }
    }

    private sealed class UnloadChunkJob : Job<bool>
    {
        public UnloadChunkJob(double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
        }

        public UnloadChunkJob(double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
        }

        protected override Task<bool> Process()
        {
            // TODO: Check if the layer has been modified.
            if (true)
            {
                // Keep it persisted.
            }

            throw new NotImplementedException();
        }
    }
}
