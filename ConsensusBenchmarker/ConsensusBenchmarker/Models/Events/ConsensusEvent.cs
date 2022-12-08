namespace ConsensusBenchmarker.Models.Events
{
    public enum ConsensusEventType { End, CreateBlock, AddTransaction }
    public class ConsensusEvent : IEvent
    {
        public int NodeId { get; set; }

        public ConsensusEventType EventType { get; set; }

        public object Data { get; set; }

        public ConsensusEvent(int nodeId, ConsensusEventType eventType, object data)
        {
            NodeId = nodeId;
            EventType = eventType;
            Data = data;
        }
    }
}
