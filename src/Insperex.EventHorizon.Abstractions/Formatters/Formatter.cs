using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Newtonsoft.Json.Serialization;

namespace Insperex.EventHorizon.Abstractions.Formatters
{
    public class Formatter
    {
        private readonly AttributeUtil _attributeUtil;
        private readonly string _topicFormat;
        private readonly string _databaseFormat;
        private readonly Dictionary<string, string> _dict;
        private readonly string _postfix;
        private readonly SnakeCaseNamingStrategy _snakeCaseNamingStrategy;

        public Formatter(AttributeUtil attributeUtil, ITopicFormatter topicFormatter, IDatabaseFormatter databaseFormatter, IFormatterPostfix formatterPostfix)
        {
            _attributeUtil = attributeUtil;
            _topicFormat = topicFormatter.GetFormat();
            _databaseFormat = databaseFormatter.GetFormat();
            _postfix = formatterPostfix.GetPostfix();
            _dict = new Dictionary<string, string>();
            _snakeCaseNamingStrategy = new SnakeCaseNamingStrategy();
        }

        public string GetTopic<TMessage>(Type type, string node = null) where TMessage : ITopicMessage => GetTopic<TMessage>(null, type, node);

        public string GetTopic<TMessage>(Assembly assembly, Type type, string node = null) where TMessage : ITopicMessage
        {
            var assemblyName = (assembly ?? type.Assembly).GetName().Name;
            var topicFormat = _attributeUtil.GetOne<StreamAttribute>(type)?.TopicFormat ?? _topicFormat;
            return ReplaceKeys(assemblyName, type, typeof(TMessage), node, "-", topicFormat);
        }

        public string GetDatabase<TCollection>(Type type) => GetDatabase<TCollection>(null, type);

        public string GetDatabase<TCollection>(Assembly assembly, Type type)
        {
            var assemblyName = (assembly ?? type.Assembly).GetName().Name;
            var attributeFormat = _attributeUtil.GetOne<StoreAttribute>(type)?.Database;
            var databaseFormat = attributeFormat ?? _databaseFormat;
            var str = ReplaceKeys(assemblyName.Split(".").First(), type, typeof(TCollection), null, "_", databaseFormat);

            if (attributeFormat == null)
                str = _snakeCaseNamingStrategy.GetPropertyName(str, false);

            return str;
        }

        private string ReplaceKeys(string assemblyName, MemberInfo type, MemberInfo wrapperType, string node, string separator, string format)
        {
            var str = format
                .Replace(EventHorizonConstants.AssemblyKey, assemblyName)
                .Replace(EventHorizonConstants.TypeKey, type.Name.Split("`")[0])
                .Replace(EventHorizonConstants.MessageKey, node ?? wrapperType.Name.Split("`")[0]);

            return _postfix == null? str : str + separator + _postfix;
        }
    }
}
