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
            if (!TypeDefinitions.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties().
                    Where(x => x.CanRead && x.CanWrite &&
                        !x.CustomAttributes.Any(y => x.GetType() != typeof(JsonIgnoreAttribute) || x.GetType() != typeof(IgnoreDataMemberAttribute))).
                    Select(x => new PropertyDefinition { PropertyInfo = x }).
                    ToDictionary(x => x.PropertyInfo.Name, x => x);
                TypeDefinitions.TryAdd(type, properties);
            }

            return properties;
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
            Func<object, byte[]> func;
            var dataLengthLength = 1;

            if (definition.SerializeFunc == null)
            {
                var propertyInfo = definition.PropertyInfo;

                if (propertyInfo.PropertyType.IsValueType)
                {
                    if (propertyInfo.PropertyType == typeof(bool) || propertyInfo.PropertyType == typeof(bool?))
                    {
                        func = (object v) => { return BitConverter.GetBytes((bool)v); };
                    }
                    else if (propertyInfo.PropertyType == typeof(Guid) || propertyInfo.PropertyType == typeof(Guid?))
                    {
                        func = (object v) => { return ((Guid)v).ToByteArray(); };
                    }
                    else if (propertyInfo.PropertyType == typeof(TimeSpan) || propertyInfo.PropertyType == typeof(TimeSpan?))
                    {
                        func = (object v) => { return BitConverter.GetBytes(((TimeSpan)v).TotalMilliseconds); };
                    }
                    else if (propertyInfo.PropertyType.IsEnum)
                    {
                        func = (object v) => { return BitConverter.GetBytes((int)v); };
                    }
                    else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                    {
                        func = (object v) => { return BitConverter.GetBytes((int)v); };
                    }
                    else if (propertyInfo.PropertyType == typeof(long) || propertyInfo.PropertyType == typeof(long?))
                    {
                        func = (object v) => { return BitConverter.GetBytes((long)v); };
                    }
                    else
                    {
                        throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                    }
                }
                else if (propertyInfo.PropertyType == typeof(string))
                {
                    func = (object v) => { return Encoding.UTF8.GetBytes(v as string); };
                    dataLengthLength = 4;
                }
                else if (propertyInfo.PropertyType.IsClass)
                {
                    func = (object v) => { SerializeObject(v, stream); return null; };
                }
                else
                {
                    throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                }

                definition.SerializeFunc = func;
                definition.DataLengthLength = dataLengthLength;
            }
            else
            {
                func = definition.SerializeFunc;
                dataLengthLength = definition.DataLengthLength;
            }

            var data = func(value);

            if (data != null)
            {
                stream.Write(dataLengthLength == 4 ? BitConverter.GetBytes(data.Length) : new byte[] { (byte)data.Length });

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

                index = DeserializeProperty(bytes, index, result, property);
            } while (index < bytes.Length);

            return (result, index);
        }

        private static int DeserializeProperty(ReadOnlyMemory<byte> bytes, int index, object obj, PropertyDefinition definition)
        {
            Func<ReadOnlyMemory<byte>, int, object> func;
            var isObject = false;
            var dataLengthLength = 1;

            if (definition.DeserializeFunc == null)
            {
                var property = definition.PropertyInfo;

                if (property.PropertyType.IsValueType)
                {
                    if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                    {
                        func = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToBoolean(d.Span); };
                    }
                    else if (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?))
                    {
                        func = (ReadOnlyMemory<byte> d, int i) => { return new Guid(d.Span); };
                    }
                    else if (property.PropertyType == typeof(TimeSpan) || property.PropertyType == typeof(TimeSpan?))
                    {
                        func = (ReadOnlyMemory<byte> d, int i) => { return TimeSpan.FromMilliseconds(BitConverter.ToDouble(d.Span)); };
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        func = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToInt32(d.Span); };
                    }
                    else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                    {
                        func = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToInt32(d.Span); };
                    }
                    else if (property.PropertyType == typeof(long) || property.PropertyType == typeof(long?))
                    {
                        func = (ReadOnlyMemory<byte> d, int i) => { return BitConverter.ToInt64(d.Span); };
                    }
                    else
                    {
                        throw new NotSupportedException($"Type not supported '{property.PropertyType.FullName}'.");
                    }
                }
                else if (property.PropertyType == typeof(string))
                {
                    func = (ReadOnlyMemory<byte> d, int i) => { return Encoding.UTF8.GetString(d.Span); };
                    dataLengthLength = 4;
                }
                else if (property.PropertyType.IsClass)
                {
                    func = (ReadOnlyMemory<byte> d, int i) => { return DeserializeObject(bytes, property.PropertyType, i); };
                    isObject = true;
                }
                else
                {
                    throw new NotSupportedException($"Type not supported '{property.PropertyType.FullName}'.");
                }

                definition.DeserializeFunc = func;
                definition.IsObject = isObject;
                definition.DataLengthLength = dataLengthLength;
            }
            else
            {
                func = definition.DeserializeFunc;
                isObject = definition.IsObject;
                dataLengthLength = definition.DataLengthLength;
            }

            object value = null;

            if (!isObject)
            {
                var dataLength = dataLengthLength == 4 
                    ? BitConverter.ToInt32(bytes.Span.Slice(index, 4))
                    : bytes.Span.Slice(index, 1)[0];
                index += dataLengthLength;

                if (dataLength > 0)
                {
                    var valueData = bytes.Slice(index, dataLength);
                    index += dataLength;

                    value = func(valueData, index);
                }
            }
            else
            {
                var propRes = (ValueTuple<object, int>)func(bytes, index);
                value = propRes.Item1;
                index = propRes.Item2;
            }

            definition.PropertyInfo.SetValue(obj, value);

            return index;
        }

        public class PropertyDefinition
        {
            public PropertyInfo PropertyInfo { get; set; }

            public Func<object, byte[]> SerializeFunc { get; set; }

            public Func<ReadOnlyMemory<byte>, int, object> DeserializeFunc { get; set; }

            public bool IsObject { get; set; }

            public byte[] NameBytes { get; set; }

            public int DataLengthLength { get; set; }
        }
    }
}
