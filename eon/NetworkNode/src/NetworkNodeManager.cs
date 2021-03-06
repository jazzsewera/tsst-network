using System.Collections.Generic;
using System.Net;
using System.Threading;
using Common.Models;
using Common.Networking.Client.Persistent;
using Common.Networking.Server.Delegates;
using Common.Networking.Server.OneShot;
using Common.Startup;
using NetworkNode.Config;
using NetworkNode.Networking.Forwarding;
using NetworkNode.Networking.LRM;
using NLog;

namespace NetworkNode
{
    public class NetworkNodeManager : IManager
    {
        private static readonly Logger LOG = LogManager.GetCurrentClassLogger();
        
        private readonly Configuration _configuration;
        private readonly IPacketForwarder _packetForwarder;
        private readonly IPersistentClientPortFactory<EonPacket> _clientClientPortFactory;
        private readonly Dictionary<string, IPersistentClientPort<EonPacket>> _clientPorts = new Dictionary<string, IPersistentClientPort<EonPacket>>();

        private readonly Dictionary<string, LinkResourceManager> _lrmPorts = new Dictionary<string, LinkResourceManager>();

        private readonly IOneShotServerPort<ManagementPacket, ResponsePacket> _fibInsertPort;

        public NetworkNodeManager(Configuration configuration,
                                  IPacketForwarder packetForwarder,
                                  IPersistentClientPortFactory<EonPacket> clientClientPortFactory)
        {
            _configuration = configuration;
            _packetForwarder = packetForwarder;
            _clientClientPortFactory = clientClientPortFactory;
            _fibInsertPort = new OneShotServerPort<ManagementPacket, ResponsePacket>(IPAddress.Parse("127.0.0.1"), configuration.NnFibInsertLocalPort);
            _fibInsertPort.RegisterReceiveRequestDelegate(OnFibInsertRequest);
        }

        public void Start()
        {
            foreach (string clientPortAlias in _configuration.LocalPortAliases)
            {
                Configuration.LrmConfiguration lrmConfiguration = _configuration.Lrms[clientPortAlias];
                _lrmPorts.Add(clientPortAlias,
                    new LinkResourceManager(clientPortAlias,
                        lrmConfiguration.RemotePortAlias,
                        lrmConfiguration.ServerAddress,
                        lrmConfiguration.LrmLinkConnectionRequestLocalPort,
                        lrmConfiguration.LrmLinkConnectionRequestRemoteAddress,
                        lrmConfiguration.LrmLinkConnectionRequestRemotePort,
                        lrmConfiguration.RcLocalTopologyRemoteAddress,
                        lrmConfiguration.RcLocalTopologyRemotePort));

                _clientPorts.Add(clientPortAlias, _clientClientPortFactory.GetPort(clientPortAlias));
                _clientPorts[clientPortAlias].ConnectPermanentlyToServer(new EonPacket.Builder().SetSrcPort(clientPortAlias).Build());
                _clientPorts[clientPortAlias].RegisterReceiveMessageEvent(_packetForwarder.ForwardPacket);
                _clientPorts[clientPortAlias].StartReceiving();

                _lrmPorts[clientPortAlias].Listen();
                _lrmPorts[clientPortAlias].SendLocalTopologyPacketAfterWakeUp();
            }

            _packetForwarder.SetClientPorts(_clientPorts);

            _fibInsertPort.Listen();

            ManualResetEvent allDone = new ManualResetEvent(false);
            allDone.WaitOne();
        }

        private ResponsePacket OnFibInsertRequest(ManagementPacket managementPacket)
        {
            _packetForwarder.ConfigureFromManagementSystem(("", managementPacket));
            return new ResponsePacket.Builder().SetRes(ResponsePacket.ResponseType.Ok).Build();
        }
    }
}
