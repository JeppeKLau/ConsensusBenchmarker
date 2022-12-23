using System.Net;

namespace ConsensusBenchmarker.Models.Events
{
    public enum ConsensusEventType { End, CreateBlock, RecieveBlock, CreateTransaction, RecieveTransaction, RequestBlockchain, RecieveBlockchain }
    public class ConsensusEvent : IEvent
    {
        public ConsensusEventType EventType { get; set; }

        public object? Data { get; set; }

        public IPAddress? Recipient { get; set; }

        public ConsensusEvent(object? data, ConsensusEventType eventType, IPAddress? recipient)
        {
            EventType = eventType;
            Data = data;
            Recipient = recipient;
        }
    }
}
