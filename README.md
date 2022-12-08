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

Plans:
+ Better durable queues
+ Command console
+ Management UI
+ Unit tests
+ Integration tests

Performance vs RabbitMQ
-----------------------
Configuration: server and client on the same pc, Intel Core i5-7200U, DDR3 Dual 16Gb, SSD

| Scenario, 1 queue, 10 clients in/out     | 100 bytes |           | 10.000 bytes |              | 1.000.000 bytes |                 |
|------------------------------------------|-----------|-----------|--------------|--------------|-----------------|-----------------|
|                                          | NaiveMq   | RabbitMq  | NaiveMq      | RabbitMq     | NaiveMq         | RabbitMq        |
| **Producers**                            |           |           |              |              |                 |                 |
| In memory message without confirmation   | 38.000    | 25.000*   |              |              |                 |                 |
| In memory message with confirmation      | 15.000    |  8.000    |              |              |                 |                 |
| Persistent message without confirmation  |  1.100    | 10.000**  |              |              |                 |                 |
| Persistent message with confirmation     |  1.100    |  1.000    |              |              |                 |                 |
| **Producers+Consumers**                  |           |           |              |              |                 |                 |
| In memory message without confirmation   | 21.000    | 31.000*   |              |              |                 |                 |
| In memory message with confirmation      |  7.700    |  6.700    |  6.600       |  6.000       |  680            |  550            |
| In memory request-response message       |  6.700    |      -    |      -       |      -       |    -            |    -            |
| Persistent message without confirmation  |  1.100    | 28.000**  |              |              |                 |                 |
| Persistent message with confirmation     |  1.100    |  1.000    |  1.000       |    500       |  400            |  130            |
| Disk only message with confirmation      |    990    |      -    |    930       |      -       |  370            |    -            |

\* RabbitMq .NET Client eats up memory and crashes, so the test is stable for about a minute.

\*\* Same as * but disk I/O is low, not sure it's durable after all.

Requirements
--------------
+ .NET Standard for Client and Service
+ .NET 6 for tests and Server
