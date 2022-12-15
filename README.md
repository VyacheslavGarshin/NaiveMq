NaiveMq
=======

.NET Standard message queue server and client.

Implemented so far:
+ All async client interface
+ Simple custom client-server messaging protocol
+ User management
+ Queue management, publish message, queue subscriptions and message consuming
+ Message publish/consume confirmation
+ Exchanges, routing with regex
+ Request-response messages with response from consumer passed to producer
+ Durable queues
+ Disk only messages for long queues with big message data
+ Queue limits by length or volume with server behaviour delay, reject or discard the message
+ Server memory limits manual/automatic
+ Management console applicaton
+ Message batching

Plans:
+ Clustering
+ Better durable queues
+ Management UI
+ Unit tests
+ Integration tests

Performance vs RabbitMQ
-----------------------
Configuration: server and client on the same pc, Intel Core i5-7200U, DDR3 Dual 16Gb, SSD

| Scenario, 1 queue, 10 clients in/out     | 100 bytes |           | 10.000 bytes |              | 1.000.000 bytes |                 |
|------------------------------------------|-----------|-----------|--------------|--------------|-----------------|-----------------|
|                                          | NaiveMq   | RabbitMq  | NaiveMq      | RabbitMq     | NaiveMq         | RabbitMq        |
| **Producers+Consumers**                  |           |           |              |              |                 |                 |
| In memory message without confirmation   | 28.000    | 31.000*   |              |              |                 |                 |
| In memory message with confirmation      |  8.100    |  6.600    |  8.000       |  6.000       |  700            |  550            |
| - 10 queues, 1 client per queue in/out   |  7.700    |           |              |              |                 |                 |
| - batch by 100 messages                  | 13.500    | 25.000    |              |              |                 |                 |
| In memory request-response message       |  7.600    |      -    |  7.300       |      -       |  690            |    -            |
| Persistent message without confirmation  |  2.000    | 28.000**  |              |              |                 |                 |
| Persistent message with confirmation     |  1.900    |  1.000    |  1.600       |    500       |  510            |  130            |
| - 10 queues, 1 client per queue in/out   |           |           |              |              |                 |                 |
| Disk only message with confirmation      |  1.700    |      -    |  1.400       |      -       |  470            |    -            |
| **Producers**                            |           |           |              |              |                 |                 |
| In memory message without confirmation   | 38.000    | 25.000*   |              |              |                 |                 |
| In memory message with confirmation      | 15.000    |  8.000    |              |              |                 |                 |
| - handle confirms in a separate handler  | 21.000    |  8.000    |              |              |                 |                 |
| Persistent message without confirmation  |  2.700    | 10.000**  |              |              |                 |                 |
| Persistent message with confirmation     |  1.900    |  1.000    |              |              |                 |                 |

\* RabbitMq .NET Client eats up all memory, so the test is stable for about a minute.

\*\* Same as * but disk I/O is low, not sure it's durable after all.

Requirements
--------------
+ .NET Standard for Client and Service
+ .NET 6 for Tests, Server and Management Console
