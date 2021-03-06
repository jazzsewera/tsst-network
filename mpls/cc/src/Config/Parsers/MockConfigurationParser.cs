using NLog;

namespace cc.Config.Parsers
{
    internal class MockConfigurationParser : IConfigurationParser
    {
        private static readonly Logger LOG = LogManager.GetCurrentClassLogger();

        public Configuration ParseConfiguration()
        {
            const string listeningAddress = "127.0.0.1";
            const short listeningPort = 3001;
            LOG.Debug($"Mock Configuration: {listeningAddress}:{listeningPort}");
            return new Configuration.Builder()
                .SetListeningAddress(listeningAddress)
                .SetListeningPort(listeningPort)
                .AddConnection(("1", "11", true))
                .AddConnection(("2", "12", true))
                .AddConnection(("3", "13", true))
                .Build();
        }
    }
}
