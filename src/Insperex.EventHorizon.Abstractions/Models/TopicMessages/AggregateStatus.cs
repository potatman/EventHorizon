namespace Insperex.EventHorizon.Abstractions.Models.TopicMessages;

public enum AggregateStatus
{
    Ok,
    CommandTimedOut,
    LoadSnapshotFailed,
    HandlerFailed,
    OnLoadFailed,
    BeforeSaveFailed,
    AfterSaveFailed,
    SaveSnapshotFailed,
    SaveEventsFailed,
}
