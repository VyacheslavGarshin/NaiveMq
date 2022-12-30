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
                var candidateDefinitionList = type.GetProperties().
                    Where(x => x.CanRead && x.CanWrite).
                    Select(x => new PropertyDefinition { PropertyInfo = x, Name = x.Name });

                var definitionList = new List<PropertyDefinition>();

                foreach (var definition in candidateDefinitionList)
                {
                    var propertyInfo = definition.PropertyInfo;

                    var ignoreDataMember = propertyInfo.GetCustomAttribute<IgnoreDataMemberAttribute>();

                    if (ignoreDataMember != null)
                    {
                        continue;
                    }

                    definitionList.Add(definition);

                    var dataMember = propertyInfo.GetCustomAttribute<DataMemberAttribute>();

                    if (dataMember != null && !string.IsNullOrEmpty(dataMember.Name))
                    {
                        definition.Name = dataMember.Name;
                    }

                    if (propertyInfo.PropertyType.IsValueType)
                    {
                        if (propertyInfo.PropertyType == typeof(bool) || propertyInfo.PropertyType == typeof(bool?))
                        {
                            definition.SerializeFunc = BoolWrite;
                            definition.DeserializeFunc = BoolRead;
                        }
                        else if (propertyInfo.PropertyType == typeof(Guid) || propertyInfo.PropertyType == typeof(Guid?))
                        {
                            definition.SerializeFunc = GuidWrite;
                            definition.DeserializeFunc = GuidRead;
                        }
                        else if (propertyInfo.PropertyType == typeof(TimeSpan) || propertyInfo.PropertyType == typeof(TimeSpan?))
                        {
                            definition.SerializeFunc = TimeSpanWrite;
                            definition.DeserializeFunc = TimeSpanRead;
                        }
                        else if (propertyInfo.PropertyType.IsEnum || (Nullable.GetUnderlyingType(propertyInfo.PropertyType)?.IsEnum ?? false))
                        {
                            definition.SerializeFunc = EnumWrite;
                            definition.DeserializeFunc = EnumRead;
                        }
                        else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                        {
                            definition.SerializeFunc = IntWrite;
                            definition.DeserializeFunc = IntRead;
                        }
                        else if (propertyInfo.PropertyType == typeof(long) || propertyInfo.PropertyType == typeof(long?))
                        {
                            definition.SerializeFunc = LongWrite;
                            definition.DeserializeFunc = LongRead;
                        }
                        else
                        {
                            throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                        }
                    }
                    else if (propertyInfo.PropertyType == typeof(string))
                    {
                        definition.SerializeFunc = StringWrite;
                        definition.DeserializeFunc = StringRead;
                    }
                    else if (propertyInfo.PropertyType.IsClass)
                    {
                        if (propertyInfo.PropertyType.GetInterfaces().Any(x => x == typeof(IList)))
                        {
                            definition.SerializeFunc = IListWrite;
                            definition.DeserializeFunc = IListRead(propertyInfo);
                        }
                        else
                        {
                            definition.SerializeFunc = SerializeObject;
                            definition.DeserializeFunc = ObjectRead(propertyInfo);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Type not supported '{propertyInfo.PropertyType.FullName}'.");
                    }
                }

                definitions = definitionList.ToDictionary(x => x.Name, x => x);
                TypeDefinitions.TryAdd(type, definitions);
            }

            return definitions;
        }

        private static void BoolWrite(object v, Stream stream)
        {
            stream.Write(BitConverter.GetBytes((bool)v));
        }

        private static (object, int) BoolRead(ReadOnlyMemory<byte> d, int i)
        {
            return (BitConverter.ToBoolean(d.Span.Slice(i, 1)), i + 1);
        }

        private static void GuidWrite(object v, Stream stream)
        {
            stream.Write(((Guid)v).ToByteArray());
        }

        private static (object, int) GuidRead(ReadOnlyMemory<byte> d, int i)
        {
            return (new Guid(d.Span.Slice(i, 16)), i + 16);
        }

        private static void TimeSpanWrite(object v, Stream stream)
        {
            stream.Write(BitConverter.GetBytes(((TimeSpan)v).TotalMilliseconds));
        }

        private static (object, int) TimeSpanRead(ReadOnlyMemory<byte> d, int i)
        {
            return (TimeSpan.FromMilliseconds(BitConverter.ToDouble(d.Span.Slice(i, 8))), i + 8);
        }

        private static void EnumWrite(object v, Stream stream)
        {
            stream.Write(BitConverter.GetBytes((int)v));
        }

        private static (object, int) EnumRead(ReadOnlyMemory<byte> d, int i)
        {
            return (BitConverter.ToInt32(d.Span.Slice(i, 4)), i + 4);
        }

        private static void IntWrite(object v, Stream stream)
        {
            stream.Write(BitConverter.GetBytes((int)v));
        }

        private static (object, int) IntRead(ReadOnlyMemory<byte> d, int i)
        {
            return (BitConverter.ToInt32(d.Span.Slice(i, 4)), i + 4);
        }

        private static void LongWrite(object v, Stream stream)
        {
            stream.Write(BitConverter.GetBytes((long)v));
        }

        private static (object, int) LongRead(ReadOnlyMemory<byte> d, int i)
        {
            return (BitConverter.ToInt64(d.Span.Slice(i, 8)), i + 8);
        }

        private static void StringWrite(object v, Stream stream)
        {
            var bytes = Encoding.UTF8.GetBytes(v as string);
            stream.Write(bytes.Length);
            stream.Write(bytes);
        }

        private static (object, int) StringRead(ReadOnlyMemory<byte> d, int i)
        {
            var length = BitConverter.ToInt32(d.Span.Slice(i, 4));
            i += 4;
            return (Encoding.UTF8.GetString(d.Span.Slice(i, length)), i + length);
        }

        private static void IListWrite(object v, Stream stream)
        {
            var collection = v as IList;

            stream.Write(collection.Count);

            foreach (var item in collection)
            {
                SerializeObject(item, stream);
            }
        }

        private static Func<ReadOnlyMemory<byte>, int, (object, int)> IListRead(PropertyInfo propertyInfo)
        {
            return (ReadOnlyMemory<byte> d, int i) =>
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

        private static Func<ReadOnlyMemory<byte>, int, (object, int)> ObjectRead(PropertyInfo propertyInfo)
        {
            return (ReadOnlyMemory<byte> d, int i) => { return DeserializeObject(d, propertyInfo.PropertyType, i); };
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
                    property.NameBytes ??= Encoding.UTF8.GetBytes(property.Name);

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

                if (property == null)
                {
                    throw new ArgumentException($"Deserialization error. Cannot find property with name '{propertyName}'.");
                }

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
            public string Name { get; set; }

            public byte[] NameBytes { get; set; }

            public PropertyInfo PropertyInfo { get; set; }

            public Action<object, Stream> SerializeFunc { get; set; }

            public Func<ReadOnlyMemory<byte>, int, ValueTuple<object, int>> DeserializeFunc { get; set; }

            public override string ToString()
            {
                return $"{PropertyInfo.DeclaringType.Name}.{PropertyInfo.Name}{(Name != PropertyInfo.Name ? $"({Name})" : string.Empty)}";
            }
        }
    }
}
