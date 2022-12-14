namespace ConsensusBenchmarker.Models.Events
{
    public enum CommunicationEventType { End, SendTransaction, SendBlock }
    public class CommunicationEvent : IEvent
    {
        public object? Data { get; set; }

        public CommunicationEventType EventType { get; set; }

        public CommunicationEvent(object? data, CommunicationEventType eventType)
        {
            Data = data;
            EventType = eventType;
        }
    }
}
