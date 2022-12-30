using CommunityToolkit.HighPerformance;
using NaiveMq.Client.Commands;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

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
                        !x.CustomAttributes.Any(y => x.GetType() != typeof(IgnoreDataMemberAttribute))).
                    Select(x => new PropertyDefinition { PropertyInfo = x }).
                    ToDictionary(x => x.PropertyInfo.Name, x => x);

                foreach (var definition in definitions.Values)
                {
                    var propertyInfo = definition.PropertyInfo;
                    
                    if (propertyInfo.PropertyType.IsValueType)
                    {
                        if (propertyInfo.PropertyType == typeof(bool) || propertyInfo.PropertyType == typeof(bool?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                stream.Write(BitConverter.GetBytes((bool)v));
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                return (BitConverter.ToBoolean(d.Span.Slice(i, 1)), i + 1);
                            };
                        }
                        else if (propertyInfo.PropertyType == typeof(Guid) || propertyInfo.PropertyType == typeof(Guid?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                stream.Write(((Guid)v).ToByteArray());
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                return (new Guid(d.Span.Slice(i, 16)), i + 16);
                            };
                        }
                        else if (propertyInfo.PropertyType == typeof(TimeSpan) || propertyInfo.PropertyType == typeof(TimeSpan?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                stream.Write(BitConverter.GetBytes(((TimeSpan)v).TotalMilliseconds));
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                return (TimeSpan.FromMilliseconds(BitConverter.ToDouble(d.Span.Slice(i, 8))), i + 8);
                            };
                        }
                        else if (propertyInfo.PropertyType.IsEnum || (Nullable.GetUnderlyingType(propertyInfo.PropertyType)?.IsEnum ?? false))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                stream.Write(BitConverter.GetBytes((int)v));
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                return (BitConverter.ToInt32(d.Span.Slice(i, 4)), i + 4);
                            };
                        }
                        else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                stream.Write(BitConverter.GetBytes((int)v));
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                return (BitConverter.ToInt32(d.Span.Slice(i, 4)), i + 4);
                            };
                        }
                        else if (propertyInfo.PropertyType == typeof(long) || propertyInfo.PropertyType == typeof(long?))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                stream.Write(BitConverter.GetBytes((long)v));
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                return (BitConverter.ToInt64(d.Span.Slice(i, 8)), i + 8);
                            };
                        }
                        else
                        {
                            throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                        }
                    }
                    else if (propertyInfo.PropertyType == typeof(string))
                    {
                        definition.SerializeFunc = (object v, Stream stream) =>
                        {
                            var bytes = Encoding.UTF8.GetBytes(v as string);
                            stream.Write(bytes.Length);
                            stream.Write(bytes);
                        };
                        definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                        {
                            var length = BitConverter.ToInt32(d.Span.Slice(i, 4));
                            i += 4;
                            return (Encoding.UTF8.GetString(d.Span.Slice(i, length)), i + length);
                        };
                    }
                    else if (propertyInfo.PropertyType.IsClass)
                    {
                        if (propertyInfo.PropertyType.GetInterfaces().Any(x => x == typeof(IList)))
                        {
                            definition.SerializeFunc = (object v, Stream stream) =>
                            {
                                var collection = v as IList;

                                stream.Write(collection.Count);

                                foreach (var item in collection)
                                {
                                    SerializeObject(item, stream);
                                }
                            };
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) =>
                            {
                                var count = BitConverter.ToInt32(d.Span.Slice(i, 4));
                                i += 4;

                                var collection = Activator.CreateInstance(propertyInfo.PropertyType) as IList;

                                for (var j = 0; j < count; j++)
                                {
                                    var res = DeserializeObject(d, propertyInfo.PropertyType.GetGenericArguments()[0], i);
                                    collection.Add(res.obj);
                                    i = res.index;
                                }

                                return (collection, i);
                            };
                        }
                        else
                        {
                            definition.SerializeFunc = SerializeObject;
                            definition.DeserializeFunc = (ReadOnlyMemory<byte> d, int i) => { return DeserializeObject(d, propertyInfo.PropertyType, i); };
                        }
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
            definition.SerializeFunc(value, stream);
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
                index++;

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
            var propRes = definition.DeserializeFunc(bytes, index);
            var value = propRes.Item1;
            index = propRes.Item2;

            definition.PropertyInfo.SetValue(obj, value);

            return index;
        }

        public class PropertyDefinition
        {
            public PropertyInfo PropertyInfo { get; set; }

            public Action<object, Stream> SerializeFunc { get; set; }

            public Func<ReadOnlyMemory<byte>, int, ValueTuple<object, int>> DeserializeFunc { get; set; }

            public byte[] NameBytes { get; set; }
        }
    }
}
