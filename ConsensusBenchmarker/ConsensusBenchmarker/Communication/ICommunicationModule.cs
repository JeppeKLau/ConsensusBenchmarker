namespace ConsensusBenchmarker.Communication
{
    public interface ICommunicationModule
    {
        public Task SendBlock();

        public Task ReceiveBlock();

        public void AddNewKnownNode(string DiscoverMessage);
    }
}
