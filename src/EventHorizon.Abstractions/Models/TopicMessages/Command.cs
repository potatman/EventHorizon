﻿using System;
using System.Text.Json;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Util;

namespace EventHorizon.Abstractions.Models.TopicMessages;

public class Command : ITopicMessage
{
    public string Id { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }

    public Command()
    {
        Id = Guid.NewGuid().ToString();
    }

    public Command(string streamId, object payload)
    {
        Id = Guid.NewGuid().ToString();
        StreamId = streamId;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload.GetType().Name;
    }
}
