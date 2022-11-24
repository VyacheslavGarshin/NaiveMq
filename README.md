# NaiveMq
.NET libraries for creating a message queue server.

Implemented so far:
+ Simple custom client-server messaging protocol
+ Queue management, publish message, queue subscriptions and message receiving
+ Durable queues (mostly)

Plans:
+ User management
+ Queue exchanges
+ Unit tests ;-)

# Performance
In comparrision to RabbitMQ:
+ In memory queue, 10 clients, 100b message, 1.5 times faster
+ Durable queue, 10 clients, 100b message, ... stay tuned 

# Requirements
+ .NET Standart for Client and Service
+ .NET 6 for tests