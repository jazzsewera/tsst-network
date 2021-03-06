﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common.Api;
using Common.Models;
using Common.Utils;
using NLog;

namespace ConnectionController
{
    public class ConnectionControllerStateNode : IConnectionControllerState
    {
        private static readonly Logger LOG = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, IApiClient<RequestPacket, ResponsePacket>> _ccPeerCoordinationClients =
            new Dictionary<string, IApiClient<RequestPacket, ResponsePacket>>();

        private readonly Dictionary<string, IApiClient<RequestPacket, ResponsePacket>> _lrmLinkConnectionRequestClients =
            new Dictionary<string, IApiClient<RequestPacket, ResponsePacket>>();

        private readonly Dictionary<string, string> _ccNames;

        private readonly IApiClient<RequestPacket, ResponsePacket> _rcRouteTableQueryClient;

        private readonly IApiClient<ManagementPacket, ResponsePacket> _nnFibInsertClient;

        public ConnectionControllerStateNode(IPAddress serverAddress,
                                             Dictionary<string, int> ccPeerCoordinationRemotePorts,
                                             Dictionary<string, int> lrmLinkConnectionRequestRemotePorts,
                                             Dictionary<string, string> ccNames,
                                             int nnFibInsertRemotePort,
                                             int rcRouteTableQueryRemotePort)
        {
            foreach ((string key, int ccPeerCoordinationRemotePort) in ccPeerCoordinationRemotePorts)
                _ccPeerCoordinationClients[key] =
                    new ApiClient<RequestPacket, ResponsePacket>(serverAddress, ccPeerCoordinationRemotePort);
            foreach ((string key, int lrmLinkConnectionRequestRemotePort) in lrmLinkConnectionRequestRemotePorts)
                _lrmLinkConnectionRequestClients[key] =
                    new ApiClient<RequestPacket, ResponsePacket>(serverAddress, lrmLinkConnectionRequestRemotePort);

            _ccNames = ccNames;

            _rcRouteTableQueryClient =
                new ApiClient<RequestPacket, ResponsePacket>(serverAddress, rcRouteTableQueryRemotePort);

            _nnFibInsertClient = new ApiClient<ManagementPacket, ResponsePacket>(serverAddress, nnFibInsertRemotePort);
        }

        public ResponsePacket OnConnectionRequest(RequestPacket requestPacket)
        {
            int id = requestPacket.Id;
            string src = requestPacket.SrcPort;
            string dst = requestPacket.DstPort;
            int sl = requestPacket.SlotsNumber;
            RequestPacket.Est est = requestPacket.Establish;

            LOG.Info($"Received CC::ConnectionRequest_req(id = {id}, src = {src}, dst = {dst}, sl = {sl}, teardown = {est})");

            LOG.Info($"Send RC::RouteTableQuery_req(id = {id}, src = {src}, dst = {dst}, sl = {sl}, teardown = {est})");
            ResponsePacket routeTableQueryResponse = _rcRouteTableQueryClient.Get(new RequestPacket.Builder()
                .SetEst(est)
                .SetId(id)
                .SetSrcPort(src)
                .SetDstPort(dst)
                .SetSlotsNumber(sl)
                .Build());

            if (routeTableQueryResponse.Res == ResponsePacket.ResponseType.ResourcesProblem)
            {
                LOG.Info("Received RC::RouteTableQuery_res(res = ResourcesProblem)");
                LOG.Info("Send ConnectionRequest_res(res = ResourcesProblem)");
                return new ResponsePacket.Builder()
                    .SetRes(ResponsePacket.ResponseType.ResourcesProblem)
                    .Build();
            }

            int rtqrId = routeTableQueryResponse.Id;
            string rtqrGateway = routeTableQueryResponse.Gateway;
            (int, int) rtqrSlots = routeTableQueryResponse.Slots;
            string dstZone = routeTableQueryResponse.DstZone;
            string gatewayOrEnd = rtqrGateway;
            LOG.Info($"Received RC::RouteTableQuery_res(id = {rtqrId}, gateway = {rtqrGateway}, slots = {rtqrSlots}, dstZone = {dstZone})");

            if (dst != rtqrGateway)
            {
                string allocDealloc = est == RequestPacket.Est.Establish ? "allocate" : "deallocate";
                LOG.Info($"Send LRM::LinkConnectionRequest_req(slots = {rtqrSlots}, {allocDealloc}, who = CC)");
                ResponsePacket linkConnectionRequestResponse = _lrmLinkConnectionRequestClients[rtqrGateway].Get(
                    new RequestPacket.Builder()
                        .SetEst(est)
                        .SetSlots(rtqrSlots)
                        .SetShouldAllocate(true)
                        .SetWhoRequests(RequestPacket.Who.Cc)
                        .Build());

                if (linkConnectionRequestResponse.Res == ResponsePacket.ResponseType.Refused)
                {
                    LOG.Info("Received LRM::LinkConnectionRequest_res(res = Refused)");
                    LOG.Info("Send CC::ConnectionRequest_res(res = Refused)");
                    return new ResponsePacket.Builder()
                        .SetRes(ResponsePacket.ResponseType.Refused)
                        .Build();
                }
                
                gatewayOrEnd = linkConnectionRequestResponse.End;
    
                LOG.Info($"Received LRM::LinkConnectionRequest_res(end = {gatewayOrEnd})");
            }
            else
            {
                ResponsePacket.ResponseType resp;
                if (est == RequestPacket.Est.Teardown)
                {
                    LOG.Debug("Dst == Gateway, LRM will be handled by the layers above");
                    LOG.Info($"Delete FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                    ResponsePacket deleteFibResponseDst = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                        .SetCommandType("delete")
                        .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                        .Build());
                    resp = deleteFibResponseDst.Res;
                }
                else
                {
                    LOG.Debug("Dst == Gateway, LRM will be handled by the layers above");
                    LOG.Info($"Insert FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                    ResponsePacket insertFibResponseDst = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                        .SetCommandType("add")
                        .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                        .Build());
                    resp = insertFibResponseDst.Res;
                }

                if (resp == ResponsePacket.ResponseType.Ok)
                {
                    LOG.Info($"Send CC::PeerCoordination_res(OK, slots = {rtqrSlots})");
                    return new ResponsePacket.Builder().SetRes(ResponsePacket.ResponseType.Ok).SetSlots(rtqrSlots)
                        .Build();
                }
            }

            // gateway == dstZone && dstZone != dst -- TODO Not implemented

            ResponsePacket.ResponseType res;

            if (est == RequestPacket.Est.Teardown)
            {
                LOG.Info($"Delete FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                ResponsePacket deleteFibResponse = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                    .SetCommandType("delete")
                    .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                    .Build());
                res = deleteFibResponse.Res;
            }
            else
            {
                LOG.Info($"Insert FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                ResponsePacket insertFibResponse = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                    .SetCommandType("add")
                    .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                    .Build());
                res = insertFibResponse.Res;
            }

            if (res == ResponsePacket.ResponseType.Ok)
            {
                LOG.Info($"Send CC::PeerCoordination_req(id = {id}, src = {gatewayOrEnd}, dst = {dst}, slots = {rtqrSlots}, teardown = {est})");

                ResponsePacket peerCoordinationResponse = _ccPeerCoordinationClients[GetCcName(gatewayOrEnd)].Get(
                    new RequestPacket.Builder()
                        .SetEst(est)
                        .SetId(id)
                        .SetSrcPort(gatewayOrEnd)
                        .SetDstPort(dst)
                        .SetSlots(rtqrSlots)
                        .Build());
                LOG.Info($"Received CC::PeerCoordination_res(res = {ResponsePacket.ResponseTypeToString(peerCoordinationResponse.Res)})");
                
                if (peerCoordinationResponse.Res == ResponsePacket.ResponseType.Ok)
                {
                    LOG.Info($"Send CC::ConnectionRequest_res(res = OK, nextZonePort = NULL, slots = {rtqrSlots})");
                    return new ResponsePacket.Builder()
                        .SetRes(ResponsePacket.ResponseType.Ok)
                        .SetNextZonePort("")
                        .SetSlots(rtqrSlots)
                        .Build();
                }

                // else
                LOG.Info($"Send CC::ConnectionRequest_res(res = Refused, nextZonePort = NULL, slots = {rtqrSlots})");
                return new ResponsePacket.Builder()
                    .SetRes(ResponsePacket.ResponseType.Refused)
                    .SetNextZonePort("")
                    .SetSlots(rtqrSlots)
                    .Build();
            }

            LOG.Info("Send CC::ConnectionRequest_res(res = Refused)");
            // else
            return new ResponsePacket.Builder()
                .SetRes(ResponsePacket.ResponseType.Refused)
                .Build();
        }

        public ResponsePacket OnPeerCoordination(RequestPacket requestPacket)
        {
            int id = requestPacket.Id;
            string src = requestPacket.SrcPort;
            string dst = requestPacket.DstPort;
            (int, int) slots = requestPacket.Slots;
            int sl = slots.Item2 - slots.Item1;
            RequestPacket.Est est = requestPacket.Establish;

            LOG.Info($"Received CC::PeerCoordination_req(id = {id}, src = {src}, dst = {dst}, slots = {slots}, teardown = {est})");

            LOG.Info($"Send RC::RouteTableQuery_req(id = {id}, src = {src}, dst = {dst}, sl = {sl}, teardown = {est})");
            ResponsePacket routeTableQueryResponse = _rcRouteTableQueryClient.Get(new RequestPacket.Builder()
                .SetEst(est)
                .SetId(id)
                .SetSrcPort(src)
                .SetDstPort(dst)
                .SetSlotsNumber(sl)
                .Build());

            if (routeTableQueryResponse.Res == ResponsePacket.ResponseType.ResourcesProblem)
            {
                LOG.Info("Received RC::RouteTableQuery_res(res = ResourcesProblem)");
                LOG.Info("Send PeerCoordination_res(res = ResourcesProblem)");
                return new ResponsePacket.Builder()
                    .SetRes(ResponsePacket.ResponseType.ResourcesProblem)
                    .Build();
            }

            int rtqrId = routeTableQueryResponse.Id;
            string rtqrGateway = routeTableQueryResponse.Gateway;
            (int, int) rtqrSlots = routeTableQueryResponse.Slots;
            string dstZone = routeTableQueryResponse.DstZone;
            string gatewayOrEnd = rtqrGateway;
            LOG.Info($"Received RC::RouteTableQuery_res(id = {rtqrId}, gateway = {rtqrGateway}, slots = {rtqrSlots}, dstZone = {dstZone})");

            if (dst != rtqrGateway)
            {
                string allocDealloc = est == RequestPacket.Est.Establish ? "allocate" : "deallocate";
                LOG.Info($"Send LRM::LinkConnectionRequest_req(slots = {rtqrSlots}, {allocDealloc}, who = CC)");
                ResponsePacket linkConnectionRequestResponse = _lrmLinkConnectionRequestClients[rtqrGateway].Get(
                    new RequestPacket.Builder()
                        .SetEst(est)
                        .SetSlots(rtqrSlots)
                        .SetShouldAllocate(true)
                        .SetWhoRequests(RequestPacket.Who.Cc)
                        .Build());

                gatewayOrEnd = linkConnectionRequestResponse.End;

                LOG.Info($"Received LRM::LinkConnectionRequest_res(end = {gatewayOrEnd})");
            }
            else
            {
                ResponsePacket.ResponseType resp;
                if (est == RequestPacket.Est.Teardown)
                {
                    LOG.Debug("Dst == Gateway, LRM will be handled by the layers above");
                    LOG.Info($"Delete FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                    ResponsePacket deleteFibResponseDst = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                        .SetCommandType("delete")
                        .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                        .Build());
                    resp = deleteFibResponseDst.Res;
                }
                else
                {
                    LOG.Debug("Dst == Gateway, LRM will be handled by the layers above");
                    LOG.Info($"Insert FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                    ResponsePacket insertFibResponseDst = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                        .SetCommandType("add")
                        .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                        .Build());
                    resp = insertFibResponseDst.Res;
                }

                if (resp == ResponsePacket.ResponseType.Ok)
                {
                    LOG.Info($"Send CC::PeerCoordination_res(OK, slots = {rtqrSlots})");
                    return new ResponsePacket.Builder().SetRes(ResponsePacket.ResponseType.Ok).SetSlots(rtqrSlots)
                        .Build();
                }
            }

            // gateway == dstZone && dstZone != dst -- TODO Not implemented
            ResponsePacket.ResponseType res;

            if (est == RequestPacket.Est.Teardown)
            {
                LOG.Info($"Delete FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                ResponsePacket deleteFibResponse = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                    .SetCommandType("delete")
                    .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                    .Build());
                res = deleteFibResponse.Res;
            }
            else
            {
                LOG.Info($"Insert FIB row [inPort = {src}, slots = ({rtqrSlots.Item1}, {rtqrSlots.Item2}), outPort = {rtqrGateway}]");
                ResponsePacket insertFibResponse = _nnFibInsertClient.Get(new ManagementPacket.Builder()
                    .SetCommandType("add")
                    .SetCommandData($"{src} {rtqrSlots.Item1} {rtqrSlots.Item2} {rtqrGateway}")
                    .Build());
                res = insertFibResponse.Res;
            }

            if (res == ResponsePacket.ResponseType.Ok)
            {
                LOG.Info($"Send CC::PeerCoordination_req(id = {id}, src = {gatewayOrEnd}, dst = {dst}, slots = {rtqrSlots}, teardown = {est})");

                ResponsePacket peerCoordinationResponse = _ccPeerCoordinationClients[GetCcName(gatewayOrEnd)].Get(
                    new RequestPacket.Builder()
                        .SetEst(est)
                        .SetId(id)
                        .SetSrcPort(gatewayOrEnd)
                        .SetDstPort(dst)
                        .SetSlots(rtqrSlots)
                        .Build());
                LOG.Info(
                    $"Received CC::PeerCoordination_res(res = {ResponsePacket.ResponseTypeToString(peerCoordinationResponse.Res)})");
                
                if (peerCoordinationResponse.Res == ResponsePacket.ResponseType.Ok)
                {
                    LOG.Info("Send CC::PeerCoordination_res(res = OK, nextZonePort = NULL)");
                    return new ResponsePacket.Builder()
                        .SetRes(ResponsePacket.ResponseType.Ok)
                        .SetNextZonePort("")
                        .SetSlots(rtqrSlots)
                        .Build();
                }

                // else
                LOG.Info("Send CC::PeerCoordination_res(res = Refused, nextZonePort = NULL)");
                return new ResponsePacket.Builder()
                    .SetRes(ResponsePacket.ResponseType.Refused)
                    .SetNextZonePort("")
                    .Build();
            }

            LOG.Info("Send CC::PeerCoordination_res(res = Refused)");
            // else
            return new ResponsePacket.Builder()
                .SetRes(ResponsePacket.ResponseType.Refused)
                .Build();
        }

        private string GetCcName(string portAlias)
        {
            foreach (KeyValuePair<string, string> ccName in _ccNames.Where(ccName =>
                Checkers.PortMatches(ccName.Key, portAlias) > -1)) return ccName.Value; // TODO: Check for matches value
            LOG.Error($"Empty ccName from GetCcName() for portAlias: {portAlias}");
            return "";
        }
    }
}
