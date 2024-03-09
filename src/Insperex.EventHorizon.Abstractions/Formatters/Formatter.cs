using System;
using System.Collections.Generic;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.Abstractions.Formatters
{
    public class Formatter
    {
        private readonly AttributeUtil _attributeUtil;
        private readonly string _topicFormat;
        private readonly string _databaseFormat;
        private readonly Dictionary<string, string> _dict;

        public Formatter(AttributeUtil attributeUtil, ITopicFormatter topicFormatter, IDatabaseFormatter databaseFormatter)
        {
            _attributeUtil = attributeUtil;
            _topicFormat = topicFormatter.GetFormat();
            _databaseFormat = databaseFormatter.GetFormat();
            _dict = new Dictionary<string, string>();
        }

        public string GetTopic<TMessage>(Type type, string node = null) where TMessage : ITopicMessage => GetTopic<TMessage>(null, type, node);

        public string GetTopic<TMessage>(Assembly assembly, Type type, string node = null) where TMessage : ITopicMessage
        {
            var topicFormat = _attributeUtil.GetOne<StreamAttribute>(type)?.TopicFormat ?? _topicFormat;
            return ReplaceKeys(assembly ?? type.Assembly, type, typeof(TMessage), node, topicFormat);
        }

        public string GetDatabase<TCollection>(Type type) => GetDatabase <TCollection>(null, type);

        public string GetDatabase<TCollection>(Assembly assembly, Type type)
        {
            var databaseFormat = _attributeUtil.GetOne<SnapshotStoreAttribute>(type)?.Database ?? _databaseFormat;
            return ReplaceKeys(assembly ?? type.Assembly, type, typeof(TCollection), null, databaseFormat);
        }

        private static string ReplaceKeys(Assembly assembly, MemberInfo type, MemberInfo messageType, string node, string format)
        {
            return format
                .Replace(EventHorizonConstants.AssemblyKey, assembly.GetName().Name)
                .Replace(EventHorizonConstants.TypeKey, type.Name)
                .Replace(EventHorizonConstants.MessageKey, node ?? messageType.Name);
        }
    }
}
