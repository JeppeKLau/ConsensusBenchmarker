namespace ConsensusBenchmarker.Models.Events
{
    public enum CommunicationEventType { End, SendTransaction, SendBlock }
    public class CommunicationEvent : IEvent
    {
        public int NodeId { get; set; }

        public object Data { get; set; }

        public CommunicationEventType EventType { get; set; }

        public CommunicationEvent(int nodeId, object data, CommunicationEventType eventType)
        {
            NodeId = nodeId;
            Data = data;
            EventType = eventType;
        }
    }
}
