
using System.Text.Json;

namespace Insperex.EventHorizon.Abstractions.Serialization.Json
{
    public class SystemJsonSerializer : ISerializer
    {
        public string Serialize<T>(T obj) => JsonSerializer.Serialize(obj);

        public T Deserialize<T>(string str) => JsonSerializer.Deserialize<T>(str);
    }
}
