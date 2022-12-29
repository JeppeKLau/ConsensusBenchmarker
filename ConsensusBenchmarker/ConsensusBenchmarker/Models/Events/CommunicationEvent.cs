namespace ConsensusBenchmarker.Models.Events
{
    public enum CommunicationEventType { End, SendTransaction, SendBlock, RequestBlockChain, RecieveBlockChain, RequestVote, CastVote, RequestHeartBeat, ReceiveHeartBeat }
    public class CommunicationEvent : IEvent
    {
        public object? Data { get; set; }

        public CommunicationEventType EventType { get; set; }

        public int? Recipient { get; set; }

        public CommunicationEvent(object? data, CommunicationEventType eventType, int? recipient)
        {
            Data = data;
            EventType = eventType;
            Recipient = recipient;
        }
    }
}
