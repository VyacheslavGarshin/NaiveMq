using NaiveMq.Client.Dto;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class SearchUsersResponse : AbstractResponse<SearchUsersResponse>
    {
        public List<User> Users { get; set; }
    }
}
