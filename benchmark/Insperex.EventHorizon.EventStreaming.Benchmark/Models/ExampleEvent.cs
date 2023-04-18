﻿using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using ProtoBuf;

namespace Insperex.EventHorizon.EventStreaming.Benchmark.Models;

[SnapshotStore("tec_test_benchmark", nameof(ExampleEvent))]
public class ExampleEvent : IEvent
{
    public string Property1 { get; set; }
    public string Property2 { get; set; }
    public string Property3 { get; set; }
    public string Property4 { get; set; }
    public string Property5 { get; set; }
    public string Property6 { get; set; }
    public string Property7 { get; set; }
    public string Property8 { get; set; }
    public string Property9 { get; set; }
    public string Property10 { get; set; }
}