using Consensus.Abstractions;
using Consensus.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Consensus.Presentation.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class ClusterController : ControllerBase
{
    private readonly ICommunicationChannel _communicationChannel;

    public ClusterController(ICommunicationChannel communicationChannel)
    {
        _communicationChannel = communicationChannel;
    }
    
    [HttpPost("command", Name = "ExecuteClientRequest")]
    public async Task<IActionResult> ExecuteClientRequestAsync(ClientRequestCommand command)
    {
        var result = await _communicationChannel.ExecuteClientRequest(command);

        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(500, result);
    }
    
    [HttpGet("meta", Name = "GetStateMachines")]
    public async Task<IActionResult> GetStateMachinesAsync()
    {
        var servers = await _communicationChannel.GetAllServersAsync();

        var serversMetadata = servers
            .Select(ks => new
            {
                ks.Id,
                ks.State,
                CurrentState = ks.StateMachine.GetCurrentState()
            });

        return Ok(serversMetadata);
    }
}