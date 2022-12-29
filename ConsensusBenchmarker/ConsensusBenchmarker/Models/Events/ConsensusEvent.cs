namespace ConsensusBenchmarker.Models.Events
{
    public enum ConsensusEventType { End, CreateBlock, RecieveBlock, CreateTransaction, RecieveTransaction, RequestBlockchain, RecieveBlockchain, RequestVote, ReceiveVote, RequestHeartBeat, ReceiveHeartBeat }
    public class ConsensusEvent : IEvent
    {
        public ConsensusEventType EventType { get; set; }

        public object? Data { get; set; }

        public int? Recipient { get; set; }

        public ConsensusEvent(object? data, ConsensusEventType eventType, int? recipient)
        {
            EventType = eventType;
            Data = data;
            Recipient = recipient;
        }
    }
}
