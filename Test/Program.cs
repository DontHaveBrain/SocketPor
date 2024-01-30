using SocketPor;
using SocketClientPor;
using ISocket;
using System.Net.Sockets;

SocketServer server = new SocketServer(null,8888);
bool b=server.Connecting();

SocketClient sc = new SocketClient();
await sc.ConnectAsync("127.0.0.1",8888);

server.MessageReceived += Server_MessageReceived;
sc.MessageReceived += Sc_MessageReceived;


sc.SendDataToServer(System.Text.Json.JsonSerializer.Serialize(new SocketMessage() {stationId=1,MesageType=1,message="Test" }));//客户端发


void Server_MessageReceived(SocketMessage obj,Socket socket)//服务器收
{
    Console.WriteLine("---------来自客户端---------");
    Console.WriteLine(obj.message+socket.LocalEndPoint.ToString());
    Console.WriteLine("---------来自客户端---------");
    server.SendMessageToClient(socket, System.Text.Json.JsonSerializer.Serialize(new SocketMessage() {   MesageType = ++obj.MesageType  }));//服务器发
}
async void Sc_MessageReceived(SocketMessage obj)//客户端收
{

    Console.WriteLine("---------来自服务器---------");
    Console.WriteLine(obj.message );
    Console.WriteLine("---------来自服务器---------"); 
}
await Task.Delay(14000);
sc.SendDataToServer(System.Text.Json.JsonSerializer.Serialize(new SocketMessage() {  MesageType = 10000}));//客户端发
Console.ReadKey();