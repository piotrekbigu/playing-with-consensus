namespace Consensus.Domain.Models;

public class RequestVoteCommand
{
    public RequestVoteCommand(int term, int candidateId, int lastLogIndex, int lastLogTerm)
    {
        Term = term;
        CandidateId = candidateId;
        LastLogIndex = lastLogIndex;
        LastLogTerm = lastLogTerm;
    }

    public int Term { get; }

    public int CandidateId { get; }

    public int LastLogIndex { get; }

    public int LastLogTerm { get; }
}