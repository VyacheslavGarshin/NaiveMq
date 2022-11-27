# NaiveMq
.NET libraries for creating a message queue server. And the default server project.

Implemented so far:
+ Simple custom client-server messaging protocol
+ Queue management, publish message, queue subscriptions and message receiving
+ Durable queues (mostly)

Plans:
+ User management
+ Queue exchanges
+ Unit tests ;-)

# Performance in comparrision to RabbitMQ
Producers:
+ In memory queue with/without confirmation, 10 clients, 100b message, 2 times faster, 
+ Durable queue with/without confirmation, 10 clients, 100b message, 2 times faster

# Requirements
+ .NET Standart for Client and Service
+ .NET 6 for tests
