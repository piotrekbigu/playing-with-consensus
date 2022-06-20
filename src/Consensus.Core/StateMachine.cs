using System.Collections.ObjectModel;
using System.Globalization;
using Consensus.Abstractions;

namespace Consensus.Core;

public class StateMachine : IStateMachine
{
    private readonly object _commandSyncRoot = new ();

    private readonly List<string> _commands = new ();
    private double _state;

    public void ApplyCommand(string command)
    {
        lock (_commandSyncRoot)
        {
            var commandParts = command.Split(' ');
            var commandOperator = commandParts[0];
            var commandValue = commandParts[1];

            switch (commandOperator)
            {
                case "+":
                    _state += double.Parse(commandValue, CultureInfo.InvariantCulture);
                    break;
                case "-":
                    _state -= double.Parse(commandValue, CultureInfo.InvariantCulture);
                    break;
                case "/":
                    _state /= double.Parse(commandValue, CultureInfo.InvariantCulture);
                    break;
                case "*":
                    _state *= double.Parse(commandValue, CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentException("Unknown operator");
            }

            _commands.Add(command);

            Console.WriteLine($"Command '{command}' applied.");
        }
    }

    public Collection<string> GetAppliedCommands()
    {
        return new Collection<string>(_commands);
    }

    public double GetCurrentState()
    {
        return _state;
    }
}