﻿using System.Net;

namespace ConsensusBenchmarker.Models.Events
{
    public enum CommunicationEventType { End, SendTransaction, SendBlock, RequestBlockChain, RecieveBlockChain }
    public class CommunicationEvent : IEvent
    {
        public object? Data { get; set; }

        public CommunicationEventType EventType { get; set; }

        public KeyValuePair<int, IPAddress>? Recipient { get; set; }

        public CommunicationEvent(object? data, CommunicationEventType eventType, KeyValuePair<int, IPAddress>? recipient)
        {
            Data = data;
            EventType = eventType;
            Recipient = recipient;
        }
    }
}
