﻿using SocketPor;
using SocketClientPor;
using ISocket;
using System.Net.Sockets;

//SocketServer server = new SocketServer();
//server.IpConfig(null, 8888);
//object _lock=new object();
//bool b=server.InitialServerConnect();

SocketClient client1 = new SocketClient();
client1.IpConfig("127.0.0.1", 8888);
await client1.InitialClientConnectAsync();
client1.MessageReceivedForServer += Client_MessageReceived;
SocketClient client2 = new SocketClient();
client2.IpConfig("127.0.0.1", 8888);
await client2.InitialClientConnectAsync();
client2.MessageReceivedForServer += Client_MessageReceived; 
SocketClient client3 = new SocketClient();
client3.IpConfig("127.0.0.1", 8888);
await client3.InitialClientConnectAsync();
client3.MessageReceivedForServer += Client_MessageReceived; 



client1.SendMessageToServer(new SocketMessage() { message="wnls"});
client2.SendMessageToServer(new SocketMessage() { message = "wnls2" });

client3.SendMessageToServer(new SocketMessage() { message = "wnls3" }); //client1.Disconnect();
await Task.Delay(2000);
client1.SendMessageToServer(new SocketMessage() { message = "wnls" });
client2.SendMessageToServer(new SocketMessage() { message = "wnls2" });

client3.SendMessageToServer(new SocketMessage() { message = "wnls3" }); //client1.Disconnect();
await Task.Delay(2000);
client1.Disconnect();
Console.ReadKey();
//void Server_MessageReceived(SocketMessage obj, Socket socket)//服务器收
//{
//    lock (_lock)
//    {
//        Console.WriteLine("---------来自客户端---------");
//        Console.WriteLine(obj.MesageType + socket.RemoteEndPoint.ToString() + ":" + obj.message);
//        Console.WriteLine("---------来自客户端---------");
//        server.SendMessageToClient(socket, (new SocketMessage() { message = "namgh" }));//服务器发
//        Console.WriteLine();
//    } 
//}
async void Client_MessageReceived(SocketMessage obj)//客户端收
{

    Console.WriteLine("---------来自服务器---------");
    Console.WriteLine(obj.MesageType + ":" + obj.message);
    Console.WriteLine("---------来自服务器---------");
    await Console.Out.WriteLineAsync();
}