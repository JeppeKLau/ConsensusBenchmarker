﻿namespace ConsensusBenchmarker.Models.Events
{
    public enum ConsensusEventType { End, CreateBlock, RecieveBlock, CreateTransaction, RecieveTransaction, RequestBlockchain, RecieveBlockchain }
    public class ConsensusEvent : IEvent
    {
        public ConsensusEventType EventType { get; set; }

        public object? Data { get; set; }

        public ConsensusEvent(object? data, ConsensusEventType eventType)
        {
            EventType = eventType;
            Data = data;
        }
    }
}