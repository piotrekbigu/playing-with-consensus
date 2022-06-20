using Consensus.Domain.Enums;
using Consensus.Domain.Models;

namespace Consensus.Abstractions;

public interface IRaftServer
{
    int Id { get; set; }

    ServerState State { get; set; }

    IStateMachine StateMachine { get; set; }

    Task StartAsync();

    Task ShutDownAsync();

    Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteCommand command);

    Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesCommand command);

    Task<ClientRequestResult> ExecuteClientRequest(ClientRequestCommand command);
}