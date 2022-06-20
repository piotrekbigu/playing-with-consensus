using System.Text.Json.Serialization;
using Consensus.Abstractions;
using Consensus.Core;
using Consensus.Core.Repositories;
using Consensus.Presentation.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(p =>
{
    p.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRaftCluster, RaftCluster>();
builder.Services.AddSingleton<ICommunicationChannel, CommunicationChannel>();

builder.Services.AddTransient<IStateMachine, StateMachine>();
builder.Services.AddTransient<IPersistentStateRepository, PersistentStateRepository>();
builder.Services.AddTransient<IVolatileStateRepository, VolatileStateRepository>();

builder.Services.AddHostedService<ClusterService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();