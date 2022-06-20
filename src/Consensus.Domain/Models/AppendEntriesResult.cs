namespace Consensus.Domain.Models;

public class AppendEntriesResult
{
    public AppendEntriesResult(bool success, int term)
    {
        Success = success;
        Term = term;
    }

    public bool Success { get;}

    public int Term { get; }
}