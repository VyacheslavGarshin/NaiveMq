﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace NaiveMq.Service.Handlers
{
    public class UpdateUserHandler : IHandler<UpdateUser, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, UpdateUser command)
        {
            context.CheckAdmin(context);

            UserEntity userEntity = null;
            UserEntity oldEntity = null;

            try
            {
                if (!context.Storage.Users.TryGetValue(command.Username, out userEntity))
                {
                    throw new ServerException(ErrorCode.UserNotFound, string.Format(ErrorCode.UserNotFound.GetDescription(), command.Username));
                }

                oldEntity = JsonConvert.DeserializeObject<UserEntity>(JsonConvert.SerializeObject(userEntity));

                if (string.Equals(context.User.Username, command.Username, StringComparison.InvariantCultureIgnoreCase)
                    && !command.IsAdministrator)
                {
                    throw new ServerException(ErrorCode.UserCannotUnsetAdministratorSelf, ErrorCode.UserCannotUnsetAdministratorSelf.GetDescription());
                }

                userEntity.IsAdministrator = command.IsAdministrator;

                if (!string.IsNullOrEmpty(command.Password))
                {
                    userEntity.PasswordHash = command.Password.ComputeHash();
                }

                await context.Storage.PersistentStorage.SaveUserAsync(userEntity, context.CancellationToken);
            }
            catch
            {
                if (userEntity != null && oldEntity != null)
                {
                    userEntity.IsAdministrator = oldEntity.IsAdministrator;
                    userEntity.PasswordHash = oldEntity.PasswordHash;
                }

                throw;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
