namespace Consensus.Abstractions;

public interface IVolatileStateRepository
{
    void Clear();

    void ClearIndexes();

    public int? GetLeaderId();

    public void SetLeaderId(int? value);

    void SetNextLogIndex(int serverId, int logIndex);

    int GetNextLogIndex(int serverId);

    void SetMatchLogIndex(int serverId, int logIndex);

    int GetMatchLogIndex(int serverId);

    void SetCommitIndex(int index);

    int GetCommitIndex();

    void SetLastApplied(int index);

    int GetLastApplied();
}