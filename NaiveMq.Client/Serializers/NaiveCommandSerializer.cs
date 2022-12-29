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
using System.Text;

namespace NaiveMq.Client.Converters
{
    public class NaiveCommandSerializer : ICommandSerializer
    {
        public static ConcurrentDictionary<Type, List<PropertyDefinition>> TypeDefinitions { get; } = new();

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
            return (ICommand)DeserializeObject(bytes.Span, type);
        }

        private void SerializeObject(object obj, Stream stream)
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

            if (!TypeDefinitions.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties().
                    Where(x => x.CanRead && x.CanWrite && !x.CustomAttributes.Any(y => x.GetType() != typeof(JsonIgnoreAttribute))).
                    Select(x => new PropertyDefinition { PropertyInfo = x }).
                    ToList();
                TypeDefinitions.TryAdd(type, properties);
            }

            //var properties = type.GetProperties().
            //    Where(x => x.CanRead && x.CanWrite && !x.CustomAttributes.Any(y => x.GetType() != typeof(JsonIgnoreAttribute))).ToList();

            foreach (var property in properties)
            {
                var value = property.PropertyInfo.GetValue(obj, null);

                if (value != null)
                {
                    var nameBytes = Encoding.UTF8.GetBytes(property.PropertyInfo.Name);
                    stream.Write(BitConverter.GetBytes(nameBytes.Length));
                    stream.Write(nameBytes);

                    var data = SerializeProperty(property, value);

                    stream.Write(BitConverter.GetBytes(data != null ? data.Length : 0));

                    if (data?.Length > 0)
                    {
                        stream.Write(data);
                    }
                }
            }
        }

        private static byte[] SerializeProperty(PropertyDefinition definition, object value)
        {
            Func<object, byte[]> func;

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
                }
                else
                {
                    throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                }

                definition.SerializeFunc = func;
            }
            else
            {
                func = definition.SerializeFunc;
            }

            return func(value);
        }

        private object DeserializeObject(ReadOnlySpan<byte> bytes, Type type)
        {
            var index = 0;
            var isNotNull = BitConverter.ToBoolean(bytes.Slice(index, 1));
            index++;

            if (!isNotNull)
            {
                return null;
            }

            var result = Activator.CreateInstance(type);

            do
            {
                var propertyNameLength = BitConverter.ToInt32(bytes.Slice(index, 4));
                index += 4;

                var propertyName = Encoding.UTF8.GetString(bytes.Slice(index, propertyNameLength));
                index += propertyNameLength;

                var dataLength = BitConverter.ToInt32(bytes.Slice(index, 4));
                index += 4;

                if (dataLength > 0)
                {
                    var data = bytes.Slice(index, dataLength);
                    index += dataLength;

                    var property = type.GetProperty(propertyName);
                    
                    var value = DeserializeProperty(data, property);

                    property.SetValue(result, value, null);
                }
            } while (index < bytes.Length);

            return result;
        }

        private static object DeserializeProperty(ReadOnlySpan<byte> data, PropertyInfo property)
        {
            object value;

            if (property.PropertyType.IsValueType)
            {
                if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                {
                    value = BitConverter.ToBoolean(data);
                }
                else if (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?))
                {
                    value = new Guid(data);
                }
                else if (property.PropertyType == typeof(TimeSpan) || property.PropertyType == typeof(TimeSpan?))
                {
                    value = TimeSpan.FromMilliseconds(BitConverter.ToDouble(data));
                }
                else if (property.PropertyType.IsEnum)
                {
                    value = BitConverter.ToInt32(data);
                }
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                {
                    value = BitConverter.ToInt32(data);
                }
                else if (property.PropertyType == typeof(long) || property.PropertyType == typeof(long?))
                {
                    value = BitConverter.ToInt64(data);
                }
                else
                {
                    throw new NotSupportedException($"Type not supported '{property.PropertyType.FullName}'.");
                }
            }
            else if (property.PropertyType == typeof(string))
            {
                value = Encoding.UTF8.GetString(data);
            }
            else
            {
                throw new NotSupportedException($"Type not supported '{property.PropertyType.FullName}'.");
            }

            return value;
        }

        public class PropertyDefinition
        {
            public PropertyInfo PropertyInfo { get; set; }

            public Func<object, byte[]> SerializeFunc { get; set; }
        }
    }
}
