using System.Collections.ObjectModel;

namespace Consensus.Abstractions;

public interface IStateMachine
{
    void ApplyCommand(string command);

    Collection<string> GetAppliedCommands();

    double GetCurrentState();
}