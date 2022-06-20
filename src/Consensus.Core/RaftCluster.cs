using System.Collections.ObjectModel;
using Consensus.Abstractions;

namespace Consensus.Core;

public class RaftCluster : IRaftCluster
{
    public RaftCluster()
    {
        Servers = new Collection<IRaftServer>();
    }

    public Collection<IRaftServer> Servers { get; }

    public void AddServer(IRaftServer raftServer)
    {
        Servers.Add(raftServer);
    }
}