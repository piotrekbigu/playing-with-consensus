using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Consensus.Abstractions;
using Consensus.Domain.Enums;
using Consensus.Domain.Models;

namespace Consensus.Core;

public class RaftServer : IRaftServer, IDisposable
{
    private readonly int _heartbeatTimeoutInMs;
    private readonly int _electionTimeoutInMs;

    private readonly SemaphoreSlim _votingSemaphore;
    private readonly SemaphoreSlim _appendEntriesSemaphore;
    private readonly SemaphoreSlim _clientRequestSemaphore;
    private readonly SemaphoreSlim _heartbeatSemaphore;
    private readonly SemaphoreSlim _electionSemaphore;

    private readonly Timer _heartbeatTimer;
    private readonly Timer _electionTimer;

    private readonly ICommunicationChannel _communicationChannel;
    private readonly IPersistentStateRepository _persistentStateRepository;
    private readonly IVolatileStateRepository _volatileStateRepository;

    private bool _disposed;

    public RaftServer(
        int id,
        IStateMachine stateMachine,
        ICommunicationChannel communicationChannel,
        IPersistentStateRepository persistentStateRepository,
        IVolatileStateRepository volatileStateRepository)
    {
        Id = id;

        _votingSemaphore = new SemaphoreSlim(1);
        _appendEntriesSemaphore = new SemaphoreSlim(1);
        _clientRequestSemaphore = new SemaphoreSlim(1);
        _heartbeatSemaphore = new SemaphoreSlim(1);
        _electionSemaphore = new SemaphoreSlim(1);

        // Each server is initialized in stopped state.
        // This state is added to simulate machine which is down and unavailable in the network.
        State = ServerState.Stopped;

        // Heartbeat timeout is initialized to default value between arbitrary defined min and max values.
        // It will be recalculated each time when server is playing leader role and sends heartbeat.
        _heartbeatTimeoutInMs = RandomNumberGenerator.GetInt32(Constants.DefaultHeartbeatTimeoutMinInMs, Constants.DefaultHeartbeatTimeoutMaxInMs);

        // Election timeout is initialized to default value between arbitrary defined min and max values.
        // It will be recalculated each time when server performs election.
        _electionTimeoutInMs = RandomNumberGenerator.GetInt32(Constants.DefaultElectionTimeoutMinInMs, Constants.DefaultElectionTimeoutMaxInMs);

        // Set dependencies
        _communicationChannel = communicationChannel;
        _persistentStateRepository = persistentStateRepository;
        _volatileStateRepository = volatileStateRepository;

        // Create timers but not start them - this must happen when server starts.
        _heartbeatTimer = new Timer(TriggerHeartbeatAsync);
        _electionTimer = new Timer(TriggerElectionAsync);

        // Assign state machine
        StateMachine = stateMachine;
    }

    ~RaftServer()
    {
        Dispose(false);
    }

    public int Id { get; set; }

    public ServerState State { get; set; }

    public IStateMachine StateMachine { get; set; }

    public async Task StartAsync()
    {
        Console.WriteLine($"[{Id}] Starting server ...");

        await ChangeStateAsync(ServerState.Follower);
    }

    public async Task ShutDownAsync()
    {
        Console.WriteLine($"[{Id}] Stopping server ...");

        await ChangeStateAsync(ServerState.Stopped);
    }

    public async Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteCommand command)
    {
        await _votingSemaphore.WaitAsync();

        var currentTerm = await _persistentStateRepository.GetCurrentTermAsync();

        Console.WriteLine($"[{Id}][{currentTerm}] Handling request vote command ...");

        if (State == ServerState.Stopped)
        {
            // If this server is stopped it cannot vote. Before requesting for vote the candidate obtains list of all servers that are up.
            // This case is therefore extremely rare and if occurs the negative vote will decrease chances for candidate to win.
            // In such a case election will be repeated and list of servers that are up will be refreshed so it should not occur again.
            return new RequestVoteResult(false, currentTerm);
        }

        if (command.Term < currentTerm)
        {
            // If candidate's term is less than current term it means that most probably this server started election first.
            // In such a case vote cannot be granted.
            return new RequestVoteResult(false, currentTerm);
        }

        if (command.Term > currentTerm)
        {
            // Following docs if candidate's term is greater than this server term it has to be assigned and server becomes follower immediately.
            await HandleRpcResponse(currentTerm, command.Term);
        }

        var votedFor = await _persistentStateRepository.GetVotedForAsync();
        var lastLogEntry = await _persistentStateRepository.GetLastLogEntryAsync();
        var lastLogIndex = lastLogEntry?.Index ?? 0;
        var lastLogTerm = lastLogEntry?.Term ?? 0;

        var voteGranted = (votedFor is null || votedFor == command.CandidateId)
                          && command.LastLogIndex >= lastLogIndex
                          && command.LastLogTerm >= lastLogTerm;

        if (voteGranted)
        {
            await _persistentStateRepository.SetVotedForAsync(command.CandidateId);
        }

        _votingSemaphore.Release();

        return new RequestVoteResult(voteGranted, currentTerm);
    }

    public async Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesCommand command)
    {
        await _appendEntriesSemaphore.WaitAsync();

        var currentTerm = await _persistentStateRepository.GetCurrentTermAsync();

        Console.WriteLine($"[{Id}][{currentTerm}] Handling append entries command ...");

        if (State == ServerState.Stopped)
        {
            return new AppendEntriesResult(false, currentTerm);
        }

        if (command.Term < currentTerm)
        {
            return new AppendEntriesResult(false, currentTerm);
        }

        var previousLogEntry = await _persistentStateRepository.GetLastLogEntryAsync();

        if (command.Entries.Any() && previousLogEntry != null && previousLogEntry.Term != command.PrevLogTerm)
        {
            return new AppendEntriesResult(false, currentTerm);
        }

        await ChangeStateAsync(ServerState.Follower);
        _volatileStateRepository.SetLeaderId(command.LeaderId);

        if (command.Entries.Any())
        {
            await _persistentStateRepository.DeleteFromIndexAsync(command.Entries[0].Index);
            await _persistentStateRepository.AppendLogsAsync(command.Entries);
        }

        var commitIndex = _volatileStateRepository.GetCommitIndex();

        if (command.LeaderCommit > commitIndex)
        {
            var from = commitIndex + 1;
            var take = command.LeaderCommit - commitIndex;
            var logsToApply = await _persistentStateRepository.GetLogEntriesFromIndexAsync(from, take);

            foreach (var logToApply in logsToApply)
            {
                var newCommitIndex = Math.Min(command.LeaderCommit, logToApply.Index);

                _volatileStateRepository.SetCommitIndex(newCommitIndex);
                StateMachine.ApplyCommand(logToApply.Command);
                _volatileStateRepository.SetLastApplied(newCommitIndex);
            }
        }

        _appendEntriesSemaphore.Release();

        return new AppendEntriesResult(true, currentTerm);
    }

    public async Task<ClientRequestResult> ExecuteClientRequest(ClientRequestCommand command)
    {
        if (State == ServerState.Leader)
        {
            await _clientRequestSemaphore.WaitAsync();

            var currentTerm = await _persistentStateRepository.GetCurrentTermAsync();
            var lastLogEntry = await _persistentStateRepository.GetLastLogEntryAsync();
            var lastLogEntryIndex = lastLogEntry?.Index ?? 0;

            var logEntry = new LogEntry(currentTerm, lastLogEntryIndex + 1, command.Command);

            await _persistentStateRepository.AppendLogAsync(logEntry);

            _clientRequestSemaphore.Release();

            var commitIndex = _volatileStateRepository.GetCommitIndex();

            while (logEntry.Index < commitIndex)
            {
                await Task.Delay(5);
                commitIndex = _volatileStateRepository.GetCommitIndex();
            }

            return new ClientRequestResult(true, "Command appended");
        }

        var leaderId = _volatileStateRepository.GetLeaderId();
        if (State == ServerState.Follower && leaderId.HasValue)
        {
            // TODO: Redirect to leader
            throw new NotImplementedException();
        }

        // TODO: Reject as there is no leader in cluster at this moment.
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static bool IsMajority(int value, int serversCount)
    {
        return value * 2 > serversCount + 1;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (!disposing)
        {
            return;
        }

        _votingSemaphore?.Dispose();
        _appendEntriesSemaphore?.Dispose();
        _clientRequestSemaphore?.Dispose();
        _heartbeatSemaphore?.Dispose();
        _heartbeatTimer?.Dispose();
        _electionTimer?.Dispose();

        _disposed = true;
    }

    private async void TriggerElectionAsync(object arg)
    {
        await ChangeStateAsync(ServerState.Candidate);

        await PerformElectionAsync();
    }

    private async void TriggerHeartbeatAsync(object arg)
    {
        await SendHeartbeatAsync(true);
    }

    private async Task SendHeartbeatAsync(bool addEntries = false)
    {
        await _heartbeatSemaphore.WaitAsync();

        var currentTerm = await _persistentStateRepository.GetCurrentTermAsync();

        Console.WriteLine($"[{Id}][{currentTerm}] Sending heartbeat ...");

        var results = new ConcurrentBag<AppendEntriesResult>();

        var commitIndex = _volatileStateRepository.GetCommitIndex();

        var lastLogEntry = await _persistentStateRepository.GetLastLogEntryAsync();
        var lastLogEntryIndex = lastLogEntry?.Index ?? 0;

        var otherAvailableServers = await GetOtherAvailableServersIdentifiersAsync();

        await Parallel.ForEachAsync(otherAvailableServers, async (serverId, cancellationToken) =>
        {
            // Get next log index for given server.
            var serverNextIndex = _volatileStateRepository.GetNextLogIndex(serverId);

            // Calculate previous log index for given server.
            var previousLogIndex = serverNextIndex - 1;

            LogEntry previousLogEntry = null;

            // If previous log index is 0 it means that there are no log entries appended to the log for given server.
            if (previousLogIndex > 0)
            {
                previousLogEntry = await _persistentStateRepository.GetLogEntryAsync(previousLogIndex);
            }

            var entries = new List<LogEntry>();

            // Populate collection of entries only if it is not an initial heartbeat upon election
            // and
            // if leader's last log entry index is greater or equal than server next index.
            if (addEntries && lastLogEntryIndex >= serverNextIndex)
            {
                // Get list of all log entries starting from server next index.
                var notAppliedEntries = await _persistentStateRepository.GetLogEntriesFromIndexAsync(serverNextIndex);
                entries = notAppliedEntries.ToList();
            }

            var command = new AppendEntriesCommand
            {
                Term = currentTerm,
                LeaderId = Id,
                Entries = new Collection<LogEntry>(entries),
                LeaderCommit = commitIndex,
                PrevLogIndex = previousLogIndex,
                PrevLogTerm = previousLogEntry?.Term ?? 0
            };

            var result = await _communicationChannel.AppendEntriesAsync(serverId, command);

            results.Add(result);

            // Perform command post processing only if it contained any entries
            if (command.Entries.Any())
            {
                if (result.Success)
                {
                    // If result is successful command log entries are considered as committed (but not applied).
                    var newServerMatchIndex = command.Entries.Last().Index;
                    var newServerNextIndex = newServerMatchIndex + 1;

                    Console.WriteLine($"[{Id}][{currentTerm}] Append entries for {serverId} succeeded. New next index: {newServerNextIndex}, new match index: {newServerMatchIndex}");

                    _volatileStateRepository.SetNextLogIndex(serverId, newServerNextIndex);
                    _volatileStateRepository.SetMatchLogIndex(serverId, newServerMatchIndex);
                }
                else if (serverNextIndex > 1)
                {
                    // If result is not successful we deal with log inconsistency.
                    // Decrement next log index for given server - with next heartbeat it will be retried.
                    // NOTE: We do this only if server next index is greater than 1 (1 is default value for each follower next index).
                    var newServerNextIndex = serverNextIndex - 1;

                    Console.WriteLine($"[{Id}][{currentTerm}] Append entries for {serverId} failed. New next index: {newServerNextIndex}");

                    _volatileStateRepository.SetNextLogIndex(serverId, newServerNextIndex);
                }
            }
        });

        var highestTerm = results
            .Max(p => p.Term);

        // If RPC request or response contains term T > currentTerm:
        // set currentTerm = T, convert to follower (§5.1)
        if (highestTerm > currentTerm)
        {
            await HandleRpcResponse(currentTerm, highestTerm);
            return;
        }

        // If there exists an N such that N > commitIndex, a majority
        // of matchIndex[i] ≥ N, and log[N].term == currentTerm:
        // set commitIndex = N (§5.3, §5.4).
        for (var n = commitIndex + 1; n <= lastLogEntryIndex; n++)
        {
            var matchCount = 1;

            foreach (var serverId in otherAvailableServers)
            {
                var serverMatchIndex = _volatileStateRepository.GetMatchLogIndex(serverId);

                if (serverMatchIndex >= n)
                {
                    matchCount++;
                }
            }

            var logEntryAtN = await _persistentStateRepository.GetLogEntryAsync(n);

            Console.WriteLine($"[{Id}][{currentTerm}] Index to commit: {logEntryAtN.Index}, match count: {matchCount}");

            if (logEntryAtN.Term == currentTerm && IsMajority(matchCount, otherAvailableServers.Count))
            {
                _volatileStateRepository.SetCommitIndex(n);
                StateMachine.ApplyCommand(logEntryAtN.Command);
                _volatileStateRepository.SetLastApplied(n);
            }
        }

        _heartbeatSemaphore.Release();
    }

    private async Task ChangeStateAsync(ServerState serverState)
    {
        State = serverState;

        switch (State)
        {
            case ServerState.Stopped:
                // If server is stopped we want to disable both timers.
                StopHeartbeatTimer();
                StopElectionTimer();
                ClearVolatileResources();
                break;
            case ServerState.Follower:
                // If serves becomes a follower we need to stop heartbeat and reset election.
                StopHeartbeatTimer();
                ResetElectionTimer();
                break;
            case ServerState.Candidate:
                // If serves becomes a candidate we need to stop heartbeat and reset election.
                StopHeartbeatTimer();
                ResetElectionTimer();
                break;
            case ServerState.Leader:
                StopElectionTimer();
                ResetHeartbeatTimer();
                await ResetLeaderAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(serverState));
        }
    }

    private async Task ResetLeaderAsync()
    {
        var otherAvailableServers = await GetOtherAvailableServersIdentifiersAsync();

        await Parallel.ForEachAsync(otherAvailableServers, async (serverId, cancellationToken) =>
        {
            var lastLogEntry = await _persistentStateRepository.GetLastLogEntryAsync();

            _volatileStateRepository.SetNextLogIndex(serverId, lastLogEntry?.Index ?? 0 + 1);
            _volatileStateRepository.SetMatchLogIndex(serverId, 0);
        });
    }

    private async Task<List<int>> GetOtherAvailableServersIdentifiersAsync()
    {
        var availableServers = (await _communicationChannel.GetAvailableServersIdentifiersAsync()).ToList();

        var otherAvailableServers = availableServers
            .Where(p => p != Id)
            .ToList();

        return otherAvailableServers;
    }

    private async Task PerformElectionAsync()
    {
        await _electionSemaphore.WaitAsync();

        var currentTerm = await _persistentStateRepository.IncrementCurrentTermAsync();

        Console.WriteLine($"[{Id}][{currentTerm}] Performing election ...");

        await _persistentStateRepository.SetVotedForAsync(Id);

        var otherAvailableServers = await GetOtherAvailableServersIdentifiersAsync();

        var votingResults = new ConcurrentBag<RequestVoteResult>();

        var lastLogEntry = await _persistentStateRepository.GetLastLogEntryAsync();
        var lastLogEntryIndex = lastLogEntry?.Index ?? 0;
        var lastLogEntryTerm = lastLogEntry?.Term ?? 0;

        await Parallel.ForEachAsync(otherAvailableServers, async (serverId, cancellationToken) =>
        {
            Console.WriteLine($"[{Id}][{currentTerm}] Requesting vote from [{serverId}] ...");
            var command = new RequestVoteCommand(currentTerm, Id, lastLogEntryIndex, lastLogEntryTerm);
            var result = await _communicationChannel.RequestVoteAsync(serverId, command);
            Console.WriteLine($"[{Id}][{currentTerm}] Requesting vote from [{serverId}] ... DONE. VoteGranted: {result.VoteGranted}, Term: {result.Term}");

            votingResults.Add(result);
        });

        var highestVoterTerm = votingResults
            .Max(p => p.Term);

        // If RPC request or response contains term T > currentTerm:
        // set currentTerm = T, convert to follower (§5.1)
        if (highestVoterTerm > currentTerm)
        {
            await HandleRpcResponse(currentTerm, highestVoterTerm);
            return;
        }

        var votesCount = 1 + votingResults
            .Count(p => p.VoteGranted);

        if (IsMajority(votesCount, otherAvailableServers.Count))
        {
            Console.WriteLine($"[{Id}][{currentTerm}] Election won. Converting into leader ...");

            _volatileStateRepository.SetLeaderId(Id);
            await ChangeStateAsync(ServerState.Leader);

            // No need to send heartbeat just after election.
            // It should happen relatively quickly taking into account
            // a magnitude difference between election and heartbeat timeout.
        }

        _electionSemaphore.Release();
    }

    private async Task HandleRpcResponse(int currentTerm, int newTerm)
    {
        Console.WriteLine($"[{Id}][{currentTerm}] RPC response term ({newTerm}) is bigger than current term ({currentTerm}). Converting into follower ...");

        await _persistentStateRepository.SetCurrentTermAsync(newTerm);
        _volatileStateRepository.SetLeaderId(null);
        await _persistentStateRepository.SetVotedForAsync(null);

        await ChangeStateAsync(ServerState.Follower);
    }

    private void StopHeartbeatTimer()
    {
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void StopElectionTimer()
    {
        _electionTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void ResetElectionTimer()
    {
        if (State == ServerState.Leader)
        {
            throw new InvalidOperationException("Server cannot reset election if it is a leader!");
        }

        _electionTimer.Change(_electionTimeoutInMs, _electionTimeoutInMs);
    }

    private void ResetHeartbeatTimer()
    {
        if (State != ServerState.Leader)
        {
            throw new InvalidOperationException("Server cannot reset heartbeat if it is not a leader!");
        }

        _heartbeatTimer.Change(0, _heartbeatTimeoutInMs);
    }

    private void ClearVolatileResources()
    {
        _volatileStateRepository.Clear();
    }
}