using Consensus.Abstractions;
using Consensus.Core;

namespace Consensus.Presentation.WebApi;

public class ClusterService : BackgroundService
{
    private readonly IRaftCluster _raftCluster;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _clusterSemaphore;

    public ClusterService(IRaftCluster raftCluster, IServiceProvider serviceProvider)
    {
        _raftCluster = raftCluster;
        _serviceProvider = serviceProvider;
        _clusterSemaphore = new SemaphoreSlim(0);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Add 5 servers - this is typical number for the production cluster.
            for (var i = 0; i < 5; i++)
            {
                AddServer(i);
            }
            
            // Start all servers at the same time.
            await Parallel.ForEachAsync(_raftCluster.Servers, stoppingToken, async (server, cancellationToken) =>
            {
                await server.StartAsync();
            });

            // Await at semaphore. Application should wait here until StopAsync is called.
            await _clusterSemaphore.WaitAsync(stoppingToken);
            
            // If application got stopped semaphore releases and code gets here. Stop all servers at the same time.
            await Parallel.ForEachAsync(_raftCluster.Servers, stoppingToken, async (server, cancellationToken) =>
            {
                await server.ShutDownAsync();
            });

            // Wait extra second to make sure base implementation of StopAsync is called after releasing sempahore.
            await Task.Delay(1000, stoppingToken);
        }
    }
    
    protected new virtual Task StopAsync(CancellationToken cancellationToken)
    {
        _clusterSemaphore.Release();

        return base.StopAsync(cancellationToken);
    }

    private void AddServer(int serverId)
    {
        var communicationChannel = _serviceProvider.GetRequiredService<ICommunicationChannel>();
        var stateMachine = _serviceProvider.GetRequiredService<IStateMachine>();
        var volatileStateRepository = _serviceProvider.GetRequiredService<IVolatileStateRepository>();
        var persistentStateRepository = _serviceProvider.GetRequiredService<IPersistentStateRepository>();
        
        _raftCluster.AddServer(new RaftServer(serverId, stateMachine, communicationChannel, persistentStateRepository, volatileStateRepository));
    }
}