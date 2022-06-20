using Consensus.Abstractions;

namespace Consensus.Core.Repositories;

public class VolatileStateRepository : IVolatileStateRepository
{
    private readonly object _nextIndexSyncRoot = new ();
    private readonly object _matchIndexSyncRoot = new ();

    private readonly Dictionary<int, int> _nextIndex;
    private readonly Dictionary<int, int> _matchIndex;

    private int? _leaderId;
    private int _commitIndex;
    private int _lastApplied;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileStateRepository"/> class.
    /// </summary>
    public VolatileStateRepository()
    {
        _nextIndex = new Dictionary<int, int>();
        _matchIndex = new Dictionary<int, int>();
    }

    public void Clear()
    {
        _commitIndex = 0;
        _lastApplied = 0;
        _nextIndex.Clear();
        _matchIndex.Clear();
    }

    public void ClearIndexes()
    {
        _nextIndex.Clear();
        _matchIndex.Clear();
    }

    public int? GetLeaderId()
    {
        return _leaderId;
    }

    public void SetLeaderId(int? value)
    {
        _leaderId = value;
    }

    public void SetNextLogIndex(int serverId, int logIndex)
    {
        lock (_nextIndexSyncRoot)
        {
            _nextIndex[serverId] = logIndex;
        }
    }

    public int GetNextLogIndex(int serverId)
    {
        return _nextIndex[serverId];
    }

    public void SetMatchLogIndex(int serverId, int logIndex)
    {
        lock (_matchIndexSyncRoot)
        {
            _matchIndex[serverId] = logIndex;
        }
    }

    public int GetMatchLogIndex(int serverId)
    {
        return _matchIndex[serverId];
    }

    public void SetCommitIndex(int index)
    {
        _commitIndex = index;
    }

    public int GetCommitIndex()
    {
        return _commitIndex;
    }

    public void SetLastApplied(int index)
    {
        _lastApplied = index;
    }

    public int GetLastApplied()
    {
        return _lastApplied;
    }
}