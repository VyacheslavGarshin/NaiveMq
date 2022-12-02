# NaiveMq
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

# Performance vs RabbitMQ

Producers:
+ In memory queue without confirmation, 10 clients, 100 chars message, ...
+ In memory queue with confirmation, 10 clients, 100 chars message, ...
+ Durable queue without confirmation, 10 clients, 100 chars message, ...
+ Durable queue with confirmation, 10 clients, 100 chars message, ...

Producers+Consumers:
+ In memory queue without confirmation, 10 clients, 100 chars message, ...
+ In memory queue with confirmation, 10 clients, 100 chars message, ...
+ Durable queue without confirmation, 10 clients, 100 chars message, ...
+ Durable queue with confirmation, 10 clients, 100 chars message, ...

# Requirements
+ .NET Standart for Client and Service
+ .NET 6 for tests and Server
