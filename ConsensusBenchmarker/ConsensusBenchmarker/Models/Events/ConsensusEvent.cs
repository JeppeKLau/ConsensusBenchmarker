namespace ConsensusBenchmarker.Models.Events
{
    public enum ConsensusEventType { End, CreateBlock, RecieveBlock, CreateTransaction, RecieveTransaction, RequestBlockchain, RecieveBlockchain }
    public class ConsensusEvent : IEvent
    {
        public ConsensusEventType EventType { get; set; }

        public object? Data { get; set; }

        public KeyValuePair<int, string>? Recipient { get; set; }

        public ConsensusEvent(object? data, ConsensusEventType eventType, KeyValuePair<int, string>? recipient)
        {
            EventType = eventType;
            Data = data;
            Recipient = recipient;
        }
    }
}
