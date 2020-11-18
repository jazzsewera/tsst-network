using Cc.Cmd.Parsers;
using Cc.Config;
using Cc.Config.Parsers;
using Cc.Networking.Client;
using Cc.Networking.Forwarders;
using Cc.Networking.Listeners;
using Cc.Networking.Tables;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Cc
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget
            {
                Name = "console",
                Layout = "[${time} | ${level:format=FirstCharacter} | ${logger}] ${message}"
            };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
            LogManager.Configuration = config;

            IConfigurationParser configurationParser = new MockConfigurationParser();
            Configuration configuration = configurationParser.ParseConfiguration();
            
            IConnectionTable connectionTable = new MockConnectionTable();
            IClientWorkerFactory clientWorkerFactory = new ClientWorkerFactory();
            IListener listener = new Listener(clientWorkerFactory, connectionTable);
            IPacketForwarder packetForwarder = new MockPacketForwarder(connectionTable);
            ICommandParser commandParser = new MockCommandParser();

            CableCloud cableCloud = new CableCloud.Builder()
                .SetConfiguration(configuration)
                .SetListener(listener)
                .SetPacketForwarder(packetForwarder)
                .SetCommandParser(commandParser)
                .Build();
            
            cableCloud.Start();
        }
    }
}