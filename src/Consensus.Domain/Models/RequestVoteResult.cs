namespace Consensus.Domain.Models;

public class RequestVoteResult
{
    public RequestVoteResult(bool voteGranted, int term)
    {
        VoteGranted = voteGranted;
        Term = term;
    }

    public bool VoteGranted { get;}

    public int Term { get; }
}