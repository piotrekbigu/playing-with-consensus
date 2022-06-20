using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Consensus.Abstractions;
using Consensus.Domain.Models;

namespace Consensus.Core.Repositories;

public class PersistentStateRepository : IPersistentStateRepository
{
    private const int RandomMin = 1;
    private const int RandomMax = 4;

    private readonly object _logSyncRoot = new ();
    private List<LogEntry> _log = new ();

    private int _currentTerm;

    private int? _votedFor;

    public async Task<int> GetCurrentTermAsync()
    {
        await SimulateTimeLapseAsync();

        return _currentTerm;
    }

    public async Task SetCurrentTermAsync(int value)
    {
        await SimulateTimeLapseAsync();

        _currentTerm = value;
    }

    public async Task<int> IncrementCurrentTermAsync()
    {
        await SimulateTimeLapseAsync();

        _currentTerm++;
        return _currentTerm;
    }

    public async Task<int?> GetVotedForAsync()
    {
        await SimulateTimeLapseAsync();

        return _votedFor;
    }

    public async Task SetVotedForAsync(int? value)
    {
        await SimulateTimeLapseAsync();

        _votedFor = value;
    }

    public async Task<LogEntry> GetLogEntryAsync(int index)
    {
        await SimulateTimeLapseAsync();

        lock (_logSyncRoot)
        {
            return _log
                .FirstOrDefault(p => p.Index == index);
        }
    }

    public async Task<LogEntry> GetLastLogEntryAsync()
    {
        await SimulateTimeLapseAsync();

        lock (_logSyncRoot)
        {
            return _log
                .OrderByDescending(p => p.Index)
                .FirstOrDefault();
        }
    }

    public async Task<IEnumerable<LogEntry>> GetLogEntriesFromIndexAsync(int from, int? take = null)
    {
        await SimulateTimeLapseAsync();

        lock (_logSyncRoot)
        {
            var logs = _log
                .OrderByDescending(p => p.Index)
                .Where(p => p.Index >= from);

            if (take.HasValue)
            {
                logs = logs.Take(take.Value);
            }

            return logs;
        }
    }

    public async Task AppendLogAsync(LogEntry entry)
    {
        await SimulateTimeLapseAsync();

        lock (_logSyncRoot)
        {
            _log.Add(entry);
        }
    }

    public async Task AppendLogsAsync(Collection<LogEntry> entries)
    {
        await SimulateTimeLapseAsync();

        lock (_logSyncRoot)
        {
            _log.AddRange(entries);
        }
    }

    public async Task DeleteFromIndexAsync(int index)
    {
        await SimulateTimeLapseAsync();

        lock (_logSyncRoot)
        {
            _log = _log.Take(index).ToList();
        }
    }

    private static Task SimulateTimeLapseAsync()
    {
        return Task.Delay(RandomNumberGenerator.GetInt32(RandomMin, RandomMax));
    }
}