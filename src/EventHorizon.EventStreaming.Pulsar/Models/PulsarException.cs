using System;

namespace EventHorizon.EventStreaming.Pulsar.Models
{
    public class PulsarException : Exception
    {
        public PulsarException(string message)
            : base(message) { }

        public PulsarException(string message, Exception inner)
            : base(message, inner) { }
    }
}
