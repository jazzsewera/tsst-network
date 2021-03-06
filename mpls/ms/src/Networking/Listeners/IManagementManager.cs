using System;
using ms.Config;

namespace ms.Networking.Listeners
{
    interface IManagementManager
    {
        /// <summary>
        /// This method starts the process of listening for incoming connections from clients - network nodes
        /// </summary>
        void StartListening();

        /// <summary>
        /// This method is called when new connection popped up and needs to be serviced
        /// </summary>
        void AcceptCallback(IAsyncResult ar);

        /// <summary>
        /// Read config, actually the Listener Socket port
        /// </summary>
        void ReadConfig(Configuration configuration);
    }
}
