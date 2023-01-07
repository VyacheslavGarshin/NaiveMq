NaiveMq
=======

.NET Standard message queue server and client.

Implemented so far:
+ All async client interface.
+ Simple custom client-server messaging protocol.
+ User management.
+ Queue management, publish message, queue subscriptions and message consuming.
+ Message publish/consume confirmation.
+ Exchanges, routing with regex.
+ Request-response messages with response from consumer passed to producer.
+ Durable queues.
+ Disk only messages for long queues with big message data.
+ Queue limits by length or volume with server behaviour delay, reject or discard the message.
+ Server memory limits manual/automatic.
+ Management console applicaton.
+ Message or any type of request batching.
+ Clustering.

Plans:
+ Release.
+ Better clustering.
+ Better durable queues.
+ Management UI.
+ Unit tests.
+ Integration tests.

Performance vs RabbitMQ (3.10.12)
-----------------------
Configuration: server and client on the same pc, Intel Core i5-7200U, DDR3 Dual 16Gb, SSD.
Scenario: 10 queue, 1 consumer, 1 producer per queue.

|                                               | 100 bytes |           | 10.000 bytes |              | 1.000.000 bytes |                 |
|-----------------------------------------------|-----------|-----------|--------------|--------------|-----------------|-----------------|
|                                               | NaiveMq   | RabbitMq  | NaiveMq      | RabbitMq     | NaiveMq         | RabbitMq        |
| **Producers+Consumers**                       |           |           |              |              |                 |                 |
| In memory message without confirmation        |   55.000  |  36.000*  |              |              |                 |                 |
| In memory message with confirmation           |   10.300  |  10.500   |      9.200   |      10.000  |           730   |           580   |
| - 100 queues                                  |    9.400  |  13.000   |              |              |                 |                 |
| - batch by 100 messages in one request        |   16.000  |         - |              |           -  |                 |               - |
| - Rabbit-like batch by 100 messages           |   15.000  |  44.000   |     12.600   |      19.000  |           650   |           450   |
| In memory request-response message            |    9.400  |         - |              |            - |                 |               - |
| Persistent message without confirmation       |    2.900  |  20.000** |              |              |                 |                 |
| Persistent message with confirmation          |    2.500  |     600   |      2.100   |        400   |           490   |           100   |
| - 100 queues                                  |    2.500  |     700   |              |              |                 |                 |
| Disk only message with confirmation           |    2.300  |         - |      1.800   |            - |           450   |               - |
| **Producers**                                 |           |           |              |              |                 |                 |
| In memory message without confirmation        |  118.000  |  45.000*  |              |              |                 |                 |
| In memory message with confirmation           |   19.000  |  16.000   |              |              |                 |                 |
| - Rabbit-like batch by 100 messages           |   96.000  |  52.000   |              |              |                 |                 |
| Persistent message without confirmation       |    4.100  |  22.000** |              |              |                 |                 |
| Persistent message with confirmation          |    2.500  |     600   |              |              |                 |                 |
| - Rabbit-like batch by 100 messages           |    2.500  |  13.000   |              |           -  |                 |               - |

\* RabbitMq .NET Client eats up all memory, so the test is stable for about a minute. Then numbers are jumping around 5.000-10.000.

\*\* Same as * and disk I/O is low, not sure the messages are persistent after all.

Requirements
--------------
+ .NET Standard for Client and Service
+ .NET 6 for Tests, Server and Management Console
