using System.Net;

namespace ConsensusBenchmarker.Models.Events
{
    public enum CommunicationEventType { End, SendTransaction, SendBlock, RequestBlockChain, RecieveBlockChain }
    public class CommunicationEvent : IEvent
    {
        public object? Data { get; set; }

        public CommunicationEventType EventType { get; set; }

        public IPAddress? Recipient { get; set; }

        public CommunicationEvent(object? data, CommunicationEventType eventType, IPAddress? ricipient)
        {
            Data = data;
            EventType = eventType;
            Recipient = ricipient;
        }
    }
}
