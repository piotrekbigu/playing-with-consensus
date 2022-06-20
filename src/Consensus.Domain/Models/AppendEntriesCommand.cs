using System.Collections.ObjectModel;

namespace Consensus.Domain.Models;

public class AppendEntriesCommand
{
    public int Term { get; init; }

    public int LeaderId { get; init; }

    public int PrevLogIndex { get; init; }

    public int PrevLogTerm { get; init; }

    public Collection<LogEntry> Entries { get; init; }

    public int LeaderCommit { get; init; }
}