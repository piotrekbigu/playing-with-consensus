using System.Collections.ObjectModel;

namespace Consensus.Abstractions;

public interface IRaftCluster
{
    Collection<IRaftServer> Servers { get; }

    void AddServer(IRaftServer raftServer);
}