using System;
using System.Collections.Generic;
using CableCloud.Config;
using Common.Models;
using Common.Networking.Server.Persistent;
using NLog;

namespace CableCloud.Networking.Forwarding
{
    public class PacketForwarder : IPacketForwarder
    {
        private static readonly Logger LOG = LogManager.GetCurrentClassLogger();

        private List<(string, string, bool)> _connectionTable;
        private IDictionary<string, IWorker<EonPacket>> _clientWorkers;

        public PacketForwarder(Configuration configuration)
        {
            _connectionTable = configuration.ConnectionTable;
        }

        public void ForwardPacket((string, EonPacket) forwardPacketTuple)
        {
            (string portAlias, EonPacket packet) = forwardPacketTuple;
            try
            {
                (string portAlias1, string portAlias2, bool isAlive) = _connectionTable.Find(connection =>
                {
                    (string port1, string port2, _) = connection;
                    return port1 == portAlias || port2 == portAlias;
                });

                if (!isAlive)
                {
                    LOG.Warn($"The connection between {portAlias1} and {portAlias2} is dead");
                    return;
                }

                if (portAlias1 == portAlias)
                {
                    LOG.Trace($"Sending {packet} to {portAlias2}");
                    _clientWorkers[portAlias2].Send(packet);
                }
                else
                {
                    LOG.Trace($"Sending {packet} to {portAlias1}");
                    _clientWorkers[portAlias1].Send(packet);
                }
            }
            catch (ArgumentNullException)
            {
                LOG.Error("SourcePortAlias in incoming EonPacket did not match any entries in connection table");
            }
        }

        public void SetClientPorts(IDictionary<string, IWorker<EonPacket>> clientWorkers)
        {
            _clientWorkers = clientWorkers;
        }

        public void SetConnectionAlive((string, string, bool) requestedConnection)
        {
            (string portAlias1, string portAlias2, bool isAlive) = requestedConnection;
            int index = _connectionTable.FindIndex(connection =>
            {
                (string port1, string port2, _) = connection;
                return port1 == portAlias1 && port2 == portAlias2 || port1 == portAlias2 && port2 == portAlias1;
            });
            LOG.Trace($"SetConnectionAlive index: {index}");
            try
            {
                _connectionTable.RemoveAt(index);
                _connectionTable.Add(requestedConnection);
            }
            catch (ArgumentOutOfRangeException)
            {
                LOG.Warn($"The requested connection: {portAlias1} <-> {portAlias2} could not be found");
            }
        }
        public void RemoveClientWorker(String portAlias)
        {
            _clientWorkers.Remove(portAlias);
        }

        public void OnClientRemoved(string key)
        {
            RemoveClientWorker(key);
        }
    }
}
