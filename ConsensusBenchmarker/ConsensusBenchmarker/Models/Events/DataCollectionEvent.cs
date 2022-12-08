namespace ConsensusBenchmarker.Models.Events
{
    public enum DataCollectionEventType { End, CollectionReady, BeginBlock, EndBlock, BeginTransaction, EndTransaction }

    public class DataCollectionEvent : IEvent
    {
        public DataCollectionEventType DataCollectionEventType { get; set; }

        public int NodeId { get; set; }

        public DataCollectionEvent(int nodeId, DataCollectionEventType type)
        {
            NodeId = nodeId;
            DataCollectionEventType = type;
        }
    }
}
