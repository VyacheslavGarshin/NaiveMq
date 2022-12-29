using CommunityToolkit.HighPerformance;
using NaiveMq.Client.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Linq;

namespace NaiveMq.Client.Converters
{
    public class NaiveCommandSerializer : ICommandSerializer
    {
        public static ConcurrentDictionary<Type, Dictionary<string, PropertyDefinition>> TypeDefinitions { get; } = new();

        public byte[] Serialize(ICommand command)
        {
            using (var stream = new MemoryStream())
            {
                SerializeObject(command, stream);
                return stream.ToArray();
            }
        }

        public (byte[] buffer, int length) Serialize(ICommand command, ArrayPool<byte> arrayPool)
        {
            using (var stream = new MemoryStream())
            {
                SerializeObject(command, stream);
                var buffer = arrayPool.Rent((int)stream.Length);
                stream.Position = 0;
                stream.Write(buffer, 0, buffer.Length);
                return (buffer, (int)stream.Length);
            }
        }

        public ICommand Deserialize(ReadOnlyMemory<byte> bytes, Type type)
        {
            return (ICommand)DeserializeObject(bytes, type).obj;
        }

        private static Dictionary<string, PropertyDefinition> GetTypeDefinition(Type type)
        {
            if (!TypeDefinitions.TryGetValue(type, out var definitions))
            {
                definitions = type.GetProperties().
                    Where(x => x.CanRead && x.CanWrite &&
                        !x.CustomAttributes.Any(y => x.GetType() != typeof(JsonIgnoreAttribute) || x.GetType() != typeof(IgnoreDataMemberAttribute))).
                    Select(x => new PropertyDefinition { PropertyInfo = x }).
                    ToDictionary(x => x.PropertyInfo.Name, x => x);

                foreach (var definition in definitions.Values)
                {
                    var propertyInfo = definition.PropertyInfo;
                    definition.DataLengthLength = 1;

                    if (propertyInfo.PropertyType.IsValueType)
                    {
                        if (propertyInfo.PropertyType == typeof(bool) || propertyInfo.PropertyType == typeof(bool?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) => { return BitConverter.GetBytes((bool)v); };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToBoolean(d.Span); };
                        }
                        else if (propertyInfo.PropertyType == typeof(Guid) || propertyInfo.PropertyType == typeof(Guid?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) => { return ((Guid)v).ToByteArray(); };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return new Guid(d.Span); };
                        }
                        else if (propertyInfo.PropertyType == typeof(TimeSpan) || propertyInfo.PropertyType == typeof(TimeSpan?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) => { return BitConverter.GetBytes(((TimeSpan)v).TotalMilliseconds); };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return TimeSpan.FromMilliseconds(BitConverter.ToDouble(d.Span)); };
                        }
                        else if (propertyInfo.PropertyType.IsEnum)
                        {
                            definition.SerializeFunc = (object v, Stream stream) => { return BitConverter.GetBytes((int)v); };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToInt32(d.Span); };
                        }
                        else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) => { return BitConverter.GetBytes((int)v); };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToInt32(d.Span); };
                        }
                        else if (propertyInfo.PropertyType == typeof(long) || propertyInfo.PropertyType == typeof(long?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) => { return BitConverter.GetBytes((long)v); };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToInt64(d.Span); };
                        }
                        else
                        {
                            throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                        }
                    }
                    else if (propertyInfo.PropertyType == typeof(string))
                    {
                        definition.SerializeFunc = (object v, Stream stream) => { return Encoding.UTF8.GetBytes(v as string); };
                        definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return Encoding.UTF8.GetString(d.Span); };
                        definition.DataLengthLength = 4;
                    }
                    else if (propertyInfo.PropertyType.IsClass)
                    {
                        definition.SerializeFunc = (object v, Stream stream) => { SerializeObject(v, stream); return null; };
                        definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return DeserializeObject(d, propertyInfo.PropertyType, i); };
                        definition.IsObject = true;
                    }
                    else
                    {
                        throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                    }
                }

                TypeDefinitions.TryAdd(type, definitions);
            }

            return definitions;
        }

        private static void SerializeObject(object obj, Stream stream)
        {
            if (obj == null)
            {
                stream.Write(BitConverter.GetBytes(false));
                return;
            }
            else
            {
                stream.Write(BitConverter.GetBytes(true));
            }

            var type = obj.GetType();

            var properties = GetTypeDefinition(type);

            foreach (var property in properties.Values)
            {
                var value = property.PropertyInfo.GetValue(obj);

                if (value != null)
                {
                    property.NameBytes ??= Encoding.UTF8.GetBytes(property.PropertyInfo.Name);

                    stream.WriteByte((byte)property.NameBytes.Length);
                    stream.Write(property.NameBytes);

                    SerializeProperty(property, value, stream);                    
                }
            }

            stream.WriteByte(0);
        }

        private static void SerializeProperty(PropertyDefinition definition, object value, Stream stream)
        {
            var data = definition.SerializeFunc(value, stream);

            if (data != null)
            {
                stream.Write(definition.DataLengthLength == 4 ? BitConverter.GetBytes(data.Length) : new byte[] { (byte)data.Length });

                if (data.Length > 0)
                {
                    stream.Write(data);
                }
            }
        }

        private static (object obj, int index) DeserializeObject(ReadOnlyMemory<byte> bytes, Type type, int index = 0)
        {
            var isNotNull = BitConverter.ToBoolean(bytes.Span.Slice(index, 1));
            index++;

            if (!isNotNull)
            {
                return (null, index);
            }

            var result = Activator.CreateInstance(type);

            var properties = GetTypeDefinition(type);

            do
            {
                var propertyNameLength = bytes.Span.Slice(index, 1)[0];
                index += 1;

                if (propertyNameLength == 0)
                {
                    break;
                }

                var propertyName = Encoding.UTF8.GetString(bytes.Span.Slice(index, propertyNameLength));
                index += propertyNameLength;

                properties.TryGetValue(propertyName, out var property);

                index = DeserializeProperty(property, bytes, index, result);
            } while (index < bytes.Length);

            return (result, index);
        }

        private static int DeserializeProperty(PropertyDefinition definition, ReadOnlyMemory<byte> bytes, int index, object obj)
        {
            object value = null;

            if (!definition.IsObject)
            {
                var dataLength = definition.DataLengthLength == 4 
                    ? BitConverter.ToInt32(bytes.Span.Slice(index, 4))
                    : bytes.Span.Slice(index, 1)[0];
                index += definition.DataLengthLength;

                if (dataLength > 0)
                {
                    var valueData = bytes.Slice(index, dataLength);
                    index += dataLength;

                    value = definition.DeserializeFunc(valueData, index);
                }
            }
            else
            {
                var propRes = (ValueTuple<object, int>)definition.DeserializeFunc(bytes, index);
                value = propRes.Item1;
                index = propRes.Item2;
            }

            definition.PropertyInfo.SetValue(obj, value);

            return index;
        }

        public class PropertyDefinition
        {
            public PropertyInfo PropertyInfo { get; set; }

            public Func<object, Stream, byte[]> SerializeFunc { get; set; }

            public Func<ReadOnlyMemory<byte>, int, object> DeserializeFunc { get; set; }

            public bool IsObject { get; set; }

            public byte[] NameBytes { get; set; }
            
            public int DataLengthLength { get; set; }
        }
    }
}
