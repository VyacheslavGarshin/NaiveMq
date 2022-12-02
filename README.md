# NaiveMq
.NET libraries for creating a message queue server. And the default server project.

Implemented so far:
+ Simple custom client-server messaging protocol
+ User management
+ Queue management, publish message, queue subscriptions and message receiving
+ Exchanges, routing with regex
+ Durable queues

Plans:
+ Better durable queues
+ Unit tests ;-)

# Performance vs RabbitMQ

Producers:
+ In memory queue with/without confirmation, 10 clients, 100 chars message, ...
+ Durable queue with/without confirmation, 10 clients, 100 chars message, ...

Producers+Consumers:
+ In memory queue with/without confirmation, 10 clients, 100 chars message, ...
+ Durable queue with/without confirmation, 10 clients, 100 chars message, ...

# Requirements
+ .NET Standart for Client and Service
+ .NET 6 for tests and Server
