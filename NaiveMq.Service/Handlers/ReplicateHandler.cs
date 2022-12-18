﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Commands;

namespace NaiveMq.Service.Handlers
{
    public class ReplicateHandler : IHandler<Replicate, Confirmation>
    {
        public Task<Confirmation> ExecuteAsync(ClientContext context, Replicate command)
        {
            context.CheckClusterAdmin(context);

            context.Reinstate = true;

            return Task.FromResult(Confirmation.Ok(command));
        }

        public void Dispose()
        {
        }
    }
}