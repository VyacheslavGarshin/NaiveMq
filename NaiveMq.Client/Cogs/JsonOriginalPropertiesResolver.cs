using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Reflection;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Ignore DataMember.Name attribute.
    /// </summary>
    public class JsonOriginalPropertiesResolver : DefaultContractResolver
    {
        /// <summary>
        /// Default instance.
        /// </summary>
        public static JsonOriginalPropertiesResolver Default { get; } = new();

        /// <inheritdoc/>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            property.PropertyName = member.Name;
            return property;
        }
    }
}
