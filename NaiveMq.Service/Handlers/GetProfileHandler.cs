﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetProfileHandler : IHandler<GetProfile, GetProfileResponse>
    {
        public Task<GetProfileResponse> ExecuteAsync(ClientContext context, GetProfile command)
        {
            context.CheckUser(context);

            return Task.FromResult(GetProfileResponse.Ok(command, (response) =>
            {
                response.Profile = new Profile
                {
                    Username = context.User.Username,
                    Administrator = context.User.Administrator
                };
            }));
        }

        public void Dispose()
        {
        }
    }
}
