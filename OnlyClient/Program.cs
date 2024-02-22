using ISocket;
using Test;

InitialMEF initialMEF = new InitialMEF();
ISocketble isocket = Test.InitialMEF.Store.compositionContainer.GetExportedValueOrDefault<ISocketble>();
isocket.IpConfig("127.0.0.1",8888);
isocket.InitialClientConnectAsync();
isocket.MessageReceivedForServer += Isocket_MessageReceivedForServer;
while (true)
{
   await Task.Delay(20);
    isocket.SendMessageToServer(new SocketMessage { message = "namgh" });
    Console.WriteLine("已发");
}


void Isocket_MessageReceivedForServer(SocketMessage obj)
{
    Console.WriteLine("来自服务器："+obj.message);
}

Console.ReadKey();