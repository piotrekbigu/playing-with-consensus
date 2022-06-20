namespace Consensus.Domain.Models;

public class LogEntry
{
    public LogEntry(int term, int index, string command)
    {
        Term = term;
        Index = index;
        Command = command;
    }

    public int Term { get; }

    public int Index { get; }

    public string Command { get; }
}