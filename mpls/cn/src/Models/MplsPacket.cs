using System.Collections.Generic;
using MessagePack;

namespace cn.Models
{
    [MessagePackObject]
    public class MplsPacket
    {
        [Key(0)] public string SourcePortAlias { get; set; }

        [Key(1)] public string DestinationPortAlias { get; set; }

        [Key(2)] public List<long> MplsLabels { get; set; }

        [Key(3)] public string Message { get; set; }

        public static byte[] ToBytes(MplsPacket mplsPacket)
        {
            return MessagePackSerializer.Serialize(mplsPacket);
        }

        public static MplsPacket FromBytes(byte[] bytes)
        {
            return MessagePackSerializer.Deserialize<MplsPacket>(bytes);
        }

        public override string ToString()
        {
            return $"[{SourcePortAlias}, {DestinationPortAlias}, [{string.Join(", ", MplsLabels)}], {Message}]";
        }

        private MplsPacket(string sourcePortAlias, string destinationPortAlias, List<long> mplsLabels, string message)
        {
            SourcePortAlias = sourcePortAlias;
            DestinationPortAlias = destinationPortAlias;
            MplsLabels = mplsLabels;
            Message = message;
        }

        /// <summary>
        /// Constructor only for MessagePack deserialization
        /// </summary>
        /// To manually create MplsPacket object, use <see cref="Builder"/>
        public MplsPacket() {}

        /// <summary>
        /// Builder for MplsPacket
        /// </summary>
        /// Sample usage
        /// <code>
        /// MplsPacket packet = new MplsPacket.Builder()
        ///     .SetSourcePortAlias("1234")
        ///     .SetDestinationPortAlias("1235")
        ///     .AddMplsLabel(100)
        ///     .AddMplsLabel(200)
        ///     .SetMessage("Hello")
        ///     .Build();
        /// </code>
        public class Builder
        {
            private string _sourcePortAlias = string.Empty;
            private string _destinationPortAlias = string.Empty;
            private List<long> _mplsLabels;
            private string _message = string.Empty;

            public Builder SetSourcePortAlias(string sourcePortAlias)
            {
                _sourcePortAlias = sourcePortAlias;
                return this;
            }

            public Builder SetDestinationPortAlias(string destinationPortAlias)
            {
                _destinationPortAlias = destinationPortAlias;
                return this;
            }

            public Builder SetMplsLabels(List<long> mplsLabels)
            {
                _mplsLabels = mplsLabels;
                return this;
            }

            public Builder AddMplsLabel(long mplsLabel)
            {
                _mplsLabels ??= new List<long>();
                _mplsLabels.Add(mplsLabel);
                return this;
            }

            public Builder SetMessage(string message)
            {
                _message = message;
                return this;
            }

            public MplsPacket Build()
            {
                _mplsLabels ??= new List<long>();
                return new MplsPacket(_sourcePortAlias, _destinationPortAlias, _mplsLabels, _message);
            }
        }
    }
}
