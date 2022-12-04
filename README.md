NaiveMq
=======

.NET libraries for creating a message queue server. And the default server project.

Implemented so far:
+ Simple custom client-server messaging protocol
+ User management
+ Queue management, publish message, queue subscriptions and message receiving, message confirmation
+ Exchanges, routing with regex
+ Request-response messages with response from consumer passed to producer
+ Durable queues

Plans:
+ Better durable queues
+ Command console
+ Management UI
+ Unit tests
+ Integration tests

Performance vs RabbitMQ
-----------------------
Configuration: server and client on the same pc, Intel Core i5-7200U, DDR3 Dual 16Gb

| Scenario, 1 queue, 10 clients in/out     | 100 chars |           | 10.000 chars |              | 1.000.000 chars |
|------------------------------------------|-----------|-----------|--------------|--------------|-----------------|-----------------|
|                                          | NaiveMq   | RabbitMq  | NaiveMq      | RabbitMq     | NaiveMq         | RabbitMq        |
| **Producers**                            |           |           |              |              |                 |                 |
| In memory message without confirmation   | 52.000    | 25.000*   |              |              |                 |                 |
| In memory message with confirmation      | 17.000    |  8.000    |              |              |                 |                 |
| Durable message without confirmation     |  2.500    | 10.000**  |              |              |                 |                 |
| Durable message with confirmation        |  2.200    |  1.200    |              |              |                 |                 |
| **Producers+Consumers**                  |           |           |              |              |                 |                 |
| In memory message without confirmation   | 21.000    | 31.000*   |              |              |                 |                 |
| In memory message with confirmation      |  8.400    |  7.000    |  1.900       |  5.000       |  60             |  500            |
| In memory request-response message       |  8.300    |      -    |              |              |                 |                 |
| Durable message without confirmation     |  1.900    | 28.000**  |              |              |                 |                 |
| Durable message with confirmation        |  1.900    |  1.200    |    800       |    500       |  40             |  130            |

\* RabbitMq .NET Client eats up memory and crashes, so the test is stable for about a minute.

\*\* Same as * but disk I/O is low, not sure it's durable after all.

Requirements
--------------
+ .NET Standart for Client and Service
+ .NET 6 for tests and Server
