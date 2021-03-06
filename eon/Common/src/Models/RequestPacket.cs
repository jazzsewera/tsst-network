using System.Collections.Generic;
using MessagePack;
using NLog;

namespace Common.Models
{
    [MessagePackObject]
    public class RequestPacket : GenericPacket
    {
        [Key(1)] public int Id;

        [Key(2)] public int SlotsNumber;

        [Key(3)] public (int, int) Slots;

        [Key(4)] public List<(int, int)> SlotsArray;

        [Key(5)] public bool ShouldAllocate;

        [Key(6)] public string SrcName;

        [Key(7)] public string DstName;

        [Key(8)] public string SrcPort;

        [Key(9)] public string DstPort;

        [Key(10)] public Who WhoRequests;

        [Key(11)] public string End;
        
        [Key(12)] public string Port1;
        
        [Key(13)] public string Port2;

        [Key(14)] public Est Establish;

        /// <summary>
        /// Constructor only for MessagePack deserialization
        /// </summary>
        /// To manually create an object, use <see cref="Builder"/>
        public RequestPacket(){}

        protected RequestPacket(int id,
                                int slotsNumber,
                                (int, int) slots,
                                List<(int, int)> slotsArray,
                                bool shouldAllocate,
                                string srcName,
                                string dstName,
                                string srcPort,
                                string dstPort,
                                Who whoRequests,
                                string end,
                                string port1,
                                string port2,
                                Est establish) : base(PacketType.Request)
        {
            Id = id;
            SlotsNumber = slotsNumber;
            Slots = slots;
            SlotsArray = slotsArray;
            ShouldAllocate = shouldAllocate;
            SrcName = srcName;
            DstName = dstName;
            SrcPort = srcPort;
            DstPort = dstPort;
            WhoRequests = whoRequests;
            End = end;
            Port1 = port1;
            Port2 = port2;
            Establish = establish;
        }

        public override string ToString()
        {
            return $"[Request, id: {Id}, slots: {Slots}, slotsArray: [{string.Join(", ", SlotsArray)}],\n" +
                   $" shouldAllocate: {ShouldAllocate}, srcName: {SrcName}, dstName: {DstName},\n" +
                   $" srcPort: {SrcPort}, dstPort: {DstPort}, who: {WhoRequestsToString(WhoRequests)}, end: {End}, establish: {Establish}]";
        }

        public override byte[] ToBytes()
        {
            return MessagePackSerializer.Serialize(this);
        }

        public class Builder
        {
            private int _id;
            private int _slotsNumber;
            private (int, int) _slots;
            private List<(int, int)> _slotsArray;
            private bool _shouldAllocate;
            private string _srcName = string.Empty;
            private string _dstName = string.Empty;
            private string _srcPort = string.Empty;
            private string _dstPort = string.Empty;
            private Who _whoRequests = Who.NotSet;
            private string _end = string.Empty;
            private string _port1 = string.Empty;
            private string _port2 = string.Empty;
            private Est _establish = Est.Establish;

            public Builder SetId(int id)
            {
                _id = id;
                return this;
            }

            public Builder SetSlotsNumber(int slotsNumber)
            {
                _slotsNumber = slotsNumber;
                return this;
            }

            public Builder SetSlots((int, int) slots)
            {
                _slots = slots;
                return this;
            }

            public Builder SetSlotsArray(List<(int, int)> slotsArray)
            {
                _slotsArray = slotsArray;
                return this;
            }

            public Builder AddSlotsToSlotsArray((int, int) slots)
            {
                _slotsArray ??= new List<(int, int)>();
                _slotsArray.Add(slots);
                return this;
            }

            public Builder SetShouldAllocate(bool shouldAllocate)
            {
                _shouldAllocate = shouldAllocate;
                return this;
            }

            public Builder SetSrcName(string srcName)
            {
                _srcName = srcName;
                return this;
            }

            public Builder SetDstName(string dstName)
            {
                _dstName = dstName;
                return this;
            }

            public Builder SetSrcPort(string srcPort)
            {
                _srcPort = srcPort;
                return this;
            }

            public Builder SetDstPort(string dstPort)
            {
                _dstPort = dstPort;
                return this;
            }

            public Builder SetWhoRequests(Who whoRequests)
            {
                _whoRequests = whoRequests;
                return this;
            }

            public Builder SetEnd(string end)
            {
                _end = end;
                return this;
            }
            
            public Builder SetPort1(string port1)
            {
                _port1 = port1;
                return this;
            }
            
            public Builder SetPort2(string port2)
            {
                _port2 = port2;
                return this;
            }

            public Builder SetEst(Est establish)
            {
                _establish = establish;
                return this;
            }

            public RequestPacket Build()
            {
                _slotsArray ??= new List<(int, int)>();
                return new RequestPacket(_id,
                    _slotsNumber,
                    _slots,
                    _slotsArray,
                    _shouldAllocate,
                    _srcName,
                    _dstName,
                    _srcPort,
                    _dstPort,
                    _whoRequests,
                    _end,
                    _port1,
                    _port2,
                    _establish
                    );
            }
        }

        public static string WhoRequestsToString(Who whoRequests)
        {
            return whoRequests switch
            {
                Who.Cc => "CC Requests",
                Who.Lrm => "LRM Requests",
                _ => "_"
            };
        }

        public enum Who
        {
            NotSet,
            Lrm,
            Cc
        }

        public enum Est
        {
            Establish,
            Teardown
        }
    }
}
