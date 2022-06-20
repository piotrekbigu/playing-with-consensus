using System.Collections.ObjectModel;
using Consensus.Domain.Models;

namespace Consensus.Abstractions;

public interface IPersistentStateRepository
{
    Task<int> GetCurrentTermAsync();

    Task SetCurrentTermAsync(int value);

    Task<int> IncrementCurrentTermAsync();

    Task<int?> GetVotedForAsync();

    Task SetVotedForAsync(int? value);

    Task<LogEntry> GetLogEntryAsync(int index);

    Task<LogEntry> GetLastLogEntryAsync();

    Task<IEnumerable<LogEntry>> GetLogEntriesFromIndexAsync(int from, int? take = null);

    Task AppendLogAsync(LogEntry entry);

    Task AppendLogsAsync(Collection<LogEntry> entries);

    Task DeleteFromIndexAsync(int index);
}