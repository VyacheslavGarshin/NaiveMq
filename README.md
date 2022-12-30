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
+ Message or any type of request batching
+ Clustering

Plans:
+ Better clustering
+ Better durable queues
+ Management UI
+ Unit tests
+ Integration tests

Performance vs RabbitMQ (3.10.12)
-----------------------
Configuration: server and client on the same pc, Intel Core i5-7200U, DDR3 Dual 16Gb, SSD

| Scenario, 10 queue, 1 consumer, 1 producer    | 100 bytes |           | 10.000 bytes |              | 1.000.000 bytes |                 |
|-----------------------------------------------|-----------|-----------|--------------|--------------|-----------------|-----------------|
|                                               | NaiveMq   | RabbitMq  | NaiveMq      | RabbitMq     | NaiveMq         | RabbitMq        |
| **Producers+Consumers**                       |           |           |              |              |                 |                 |
| In memory message without confirmation        |           |       *   |              |              |                 |                 |
| In memory message with confirmation           |           |           |              |              |                 |                 |
| - 100 queues                                  |           |           |              |              |                 |                 |
| - batch by 100 messages                       |           |           |              |              |                 |                 |
| - handle confirms in a separate handler       |           |      -    |              |      -       |                 |    -            |
| In memory request-response message            |           |      -    |              |      -       |                 |    -            |
| Persistent message without confirmation       |           |       **  |              |              |                 |                 |
| Persistent message with confirmation          |           |           |              |              |                 |                 |
| - 100 queues                                  |           |           |              |              |                 |                 |
| Disk only message with confirmation           |           |      -    |              |      -       |                 |    -            |
| **Producers**                                 |           |           |              |              |                 |                 |
| In memory message without confirmation        |           |       *   |              |              |                 |                 |
| In memory message with confirmation           |           |           |              |              |                 |                 |
| - handle confirms in a separate handler       |           |      -    |              |              |                 |                 |
| Persistent message without confirmation       |           |       **  |              |              |                 |                 |
| Persistent message with confirmation          |           |           |              |              |                 |                 |

\* RabbitMq .NET Client eats up all memory, so the test is stable for about a minute. Then numbers are around 15.000.

\*\* Same as * and disk I/O is low, not sure the messages are persistent after all.

Requirements
--------------
+ .NET Standard for Client and Service
+ .NET 6 for Tests, Server and Management Console
