# playing-with-consensus

## Assumptions

* RAFT algorithm to be used. Research points out that it is one of the most common algorithms used worldwide.
  Even Apache Kafka is about to go towards its variant called KRAFT.

* The main aim of this implementation is to demonstrate only core concepts of the RAFT consensus algorithm.
  For simplicity sake implementation was done with following assumptions:
  * Cluster and servers will utilize shared "communication channel" to perform any kind of operations and RPCs (instead of e.g. gRPC or HTTP calls).
  * Cluster is initially created with fixed configuration and number of servers.
  * Election and heartbeat values are bigger in order to be able observe how election and replication works.

## Project structure:

Solution is divided into following sub-projects:
* Consensus.Abstractions - contains interfaces used in solution.
* Consensus.Domain - contains classes representing commands, results, etc.
* Consensus.Core - contains definition of cluster, server, repositories, etc.
* Consensus.Presentation.WebApi - contains web API used as a host for background service representing RAFT cluster. It also exposes a few endpoints used to interact with the cluster:
  * /cluster/command - used to post client command to the current leader.
  * /cluster/meta - used to obtain metadata about servers in the cluster (including current state machine state).

## How to test it?

Simply start the application and observe logs. By calling /cluster/command and then /cluster/meta one can observe how cluster reacts when new command is posted by the client.

## TODOs:
* Add POST /cluster/servers/{serverId} - shutdown endpoint to test case when one of servers goes down.
* Add POST /cluster/servers/{serverId} - start endpoint to test case when one of servers starts.
* Add POST /cluster/servers - endpoint to test case when new server is added
* Build mechanisms using CancellationToken to stop tasks in case they interfere (e.g. validate heartbeat arrives at the moment when election is performed).
* Build unit tests.