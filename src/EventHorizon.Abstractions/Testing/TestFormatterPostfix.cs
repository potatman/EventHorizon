using EventHorizon.Abstractions.Formatters;

namespace EventHorizon.Abstractions.Testing
{
    public class TestFormatterPostfix(string postfix) : IFormatterPostfix
    {
        public string GetPostfix() => postfix;
    }
}
