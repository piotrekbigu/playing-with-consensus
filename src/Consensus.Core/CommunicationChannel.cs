using System.Collections.ObjectModel;
using Consensus.Abstractions;
using Consensus.Domain.Enums;
using Consensus.Domain.Models;

namespace Consensus.Core;

public class CommunicationChannel : ICommunicationChannel
{
    private readonly IRaftCluster _raftCluster;

    public CommunicationChannel(IRaftCluster raftCluster)
    {
        _raftCluster = raftCluster;
    }

    public Task<IEnumerable<int>> GetAvailableServersIdentifiersAsync()
    {
        var availableServers = GetAvailableServers()
            .Select(ks => ks.Id);

        return Task.FromResult(availableServers);
    }

    public Task<Collection<IRaftServer>> GetAllServersAsync()
    {
        return Task.FromResult(_raftCluster.Servers);
    }

    public Task<RequestVoteResult> RequestVoteAsync(int serverId, RequestVoteCommand command)
    {
        var matchingServer = GetAvailableServers()
            .FirstOrDefault(p => p.Id == serverId);

        if (matchingServer is null)
        {
            throw new InvalidOperationException($"Server with id {serverId} is not available!");
        }

        return matchingServer.HandleRequestVoteAsync(command);
    }

    public Task<AppendEntriesResult> AppendEntriesAsync(int serverId, AppendEntriesCommand command)
    {
        var matchingServer = GetAvailableServers()
            .FirstOrDefault(p => p.Id == serverId);

        if (matchingServer is null)
        {
            throw new InvalidOperationException($"Server with id {serverId} is not available!");
        }

        return matchingServer.HandleAppendEntriesAsync(command);
    }

    public Task<ClientRequestResult> ExecuteClientRequest(ClientRequestCommand command)
    {
        var availableServers = GetAvailableServers();

        var leader = availableServers
            .FirstOrDefault(p => p.State == ServerState.Leader);

        if (leader is null)
        {
            return Task.FromResult(new ClientRequestResult(false, "No leader available"));
        }

        return leader.ExecuteClientRequest(command);
    }

    private IEnumerable<IRaftServer> GetAvailableServers()
    {
        return _raftCluster.Servers
            .Where(p => p.State != ServerState.Stopped);
    }
}