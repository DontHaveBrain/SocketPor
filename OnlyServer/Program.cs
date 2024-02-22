// See https://aka.ms/new-console-template for more information
using ISocket;
using Test;
 
InitialMEF initialMEF = new InitialMEF();
ISocketble isocket= Test.InitialMEF.Store.compositionContainer.GetExportedValueOrDefault<ISocketble>();
isocket.IpConfig(null,8888);
isocket.InitialServerConnect();
isocket.MessageReceivedForClient += Isocket_MessageReceivedForClient;

void Isocket_MessageReceivedForClient(SocketMessage arg1, System.Net.Sockets.Socket arg2)
{
    Console.WriteLine(  "客户端"+arg2.RemoteEndPoint+":"+arg1.message);
   isocket.SendMessageToClient(arg2, new SocketMessage { message = "dllm" }); 
}

Console.ReadKey();