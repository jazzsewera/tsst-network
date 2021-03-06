﻿using System.Collections.Generic;
using cn.Networking.Delegates;

namespace cn
{
    public interface IClientNodeManager
    {
        public void Start();
        public void RegisterReceiveMessageEvent(ReceiveMessage receiveMessage);
        public void Send(string mplsOutLabel, string message, (List<long>, string) labels);
    }
}
