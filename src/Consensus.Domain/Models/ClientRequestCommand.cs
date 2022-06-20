namespace Consensus.Domain.Models;

public class ClientRequestCommand
{
    public ClientRequestCommand(string command)
    {
        Command = command;
    }

    public string Command { get; }
}