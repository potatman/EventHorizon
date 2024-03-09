using System;
using Insperex.EventHorizon.Abstractions.Formatters;

namespace Insperex.EventHorizon.Abstractions.Testing
{
    public class TestFormatterWrapper : IDatabaseFormatter, ITopicFormatter
    {
        private readonly IFormatter _formatter;
        private readonly string _postfix;

        public TestFormatterWrapper(IFormatter formatter, string postfix)
        {
            _formatter = formatter;
            _postfix = postfix;
        }

        public string GetFormat() => _formatter.GetFormat() + _postfix;
    }
}
