namespace cn.Utils
{
    interface IUserInterface
    {
        /// <summary>Accept destination addres and user's message
        /// entered by user</summary>
        public (string, string) EnterReceiverAndMessage();
    }
}