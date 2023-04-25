namespace Insperex.EventHorizon.EventStore.MongoDb.Models
{
    public enum WriteConcernLevel
    {
        Acknowledged,
        Unacknowledged,
        W1,
        W2,
        W3,
        Majority
    }
}
