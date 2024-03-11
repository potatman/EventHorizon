using System;
using Insperex.EventHorizon.Abstractions.Formatters;

namespace Insperex.EventHorizon.Abstractions.Testing
{
    public class TestFormatterPostfix(string postfix) : IFormatterPostfix
    {
        public string GetPostfix() => postfix;
    }
}
