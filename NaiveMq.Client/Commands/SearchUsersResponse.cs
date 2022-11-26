using NaiveMq.Client.Entities;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class SearchUsersResponse : AbstractResponse<SearchUsersResponse>
    {
        public List<UserEntity> Users { get; set; }
    }
}
