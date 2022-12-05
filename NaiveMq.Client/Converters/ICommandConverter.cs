using NaiveMq.Client.Commands;
using System;

namespace NaiveMq.Client.Converters
{
    public interface ICommandConverter
    {
        byte[] Serialize(ICommand command);

        ICommand Deserialize(byte[] bytes, Type type);
    }
}
