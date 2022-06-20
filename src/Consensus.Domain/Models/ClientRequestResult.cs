namespace Consensus.Domain.Models;

public class ClientRequestResult
{
    public ClientRequestResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get;}

    public string Message { get;}
}