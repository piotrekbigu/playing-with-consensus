using System.Collections.ObjectModel;
using Consensus.Domain.Models;

namespace Consensus.Abstractions;

public interface ICommunicationChannel
{
    Task<IEnumerable<int>> GetAvailableServersIdentifiersAsync();

    Task<Collection<IRaftServer>> GetAllServersAsync();

    Task<RequestVoteResult> RequestVoteAsync(int serverId, RequestVoteCommand command);

    Task<AppendEntriesResult> AppendEntriesAsync(int serverId, AppendEntriesCommand command);

    Task<ClientRequestResult> ExecuteClientRequest(ClientRequestCommand command);
}