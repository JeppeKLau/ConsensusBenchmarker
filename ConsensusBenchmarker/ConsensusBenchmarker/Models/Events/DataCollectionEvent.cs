namespace ConsensusBenchmarker.Models.Events
{
    public enum DataCollectionEventType { End, CollectionReady, BeginBlock, EndBlock, BeginTransaction, EndTransaction }

    public class DataCollectionEvent : IEvent
    {
        public DataCollectionEventType EventType { get; set; }

        public int NodeId { get; set; }

        public object? Data { get; set; }

        public DataCollectionEvent(int nodeId, DataCollectionEventType type, object? data)
        {
            NodeId = nodeId;
            EventType = type;
            Data = data;
        }
    }
}
