using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ISocket
{
    public interface ISocketble
    {
        public event Action<SocketMessage> MessageReceivedForServer;
        public event Action<SocketMessage, Socket> MessageReceivedForClient;
        public void IpConfig(string IPAddress, int port); 
        public  Task InitialClientConnectAsync();
        public bool InitialServerConnect();
        public  Task SendMessageToServer(SocketMessage message);
        public Task SendMessageToClient(Socket clientSocket, SocketMessage message);
        public void Disconnect();
    }
}
