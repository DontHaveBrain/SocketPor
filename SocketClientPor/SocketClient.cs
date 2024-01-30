using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using ISocket;

namespace SocketClientPor
{
    public class SocketClient
    {
        //用于交互的socket
        private Socket _clientSocket;
        //交互数据(byte)
        private byte[] _buffer = new byte[1024];
        //交互数据(string)
        private string MsgStore=string.Empty;
        //数据接收事件
        public event Action<SocketMessage> MessageReceived;
        //连接实例
        public static SocketClient intance;
        //获取实例
        public static SocketClient getIntance()
        {
            if (intance != null)
            {
                return intance;
            }
            else
            {
                intance = new SocketClient();
                return intance;
            }
        }

         
        public async Task ConnectAsync(string IPAddress, int port)
        {
            try
            {
                if (_clientSocket != null)
                {
                    _clientSocket.Close();
                }
            }
            catch { }
            CancellationTokenSource cts = new CancellationTokenSource();
            try
            {
                IPAddress ipAddress = System.Net.IPAddress.Parse(IPAddress);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                _clientSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.Connect(remoteEP);
                await Console.Out.WriteLineAsync($"已连接到服务器 {IPAddress}:{port}");
                Task.Factory.StartNew(async () => HeartJump(_clientSocket));
                StartReceiving(_clientSocket); 
            }
            catch (Exception ex)
            {
                cts.Cancel();
                if (_clientSocket != null)
                {
                    try
                    {
                        _clientSocket.Close();
                    }
                    catch (Exception exx)
                    {
                        await Console.Out.WriteLineAsync($"连接异常:{exx.Message}");
                    }
                }
                await Console.Out.WriteLineAsync($"无法连接到服务器，正在尝试重新连接...");
                await Task.Delay(5000);
                ConnectAsync(IPAddress, port);
            }

        }
        public async Task SendDataToServer(string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                Task.Factory.StartNew(() =>
                {
                    _clientSocket.BeginSend(data, 0, data.Length, SocketFlags.None, null, _clientSocket);
                });
                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                Console.WriteLine("发生异常：{0}", ex.Message);
            }
        }
        private void StartReceiving(Socket clientSocket)
        {
            try
            {
                clientSocket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), clientSocket);
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"接收服务器 {clientSocket.RemoteEndPoint} ——数据异常，错误信息：{ex.Message}");
            } 
        }


        private void ReceiveCallback(IAsyncResult ar)
        {
            Socket clientSocket = (Socket)ar.AsyncState; 
            try
            {
                int bytesRead = clientSocket.EndReceive(ar);

                if (bytesRead > 0)
                {
                    MsgStore += Encoding.UTF8.GetString(_buffer, 0, bytesRead);
                    if (!MsgStore.StartsWith("{"))
                    {
                        MsgStore = "";
                        return;
                    }
                    
                    if (!(MsgStore.EndsWith("}") || MsgStore.EndsWith("\r\n") || MsgStore.EndsWith("\r") || MsgStore.EndsWith("\n")))
                    {
                        return;
                    }
                    string strees = MsgStore;
                    MsgStore = "";
                    string[] Msgs = strees.Split('\n').Where(s => !string.IsNullOrEmpty(s)).Where(x => (x.StartsWith('{') && x.EndsWith('}'))).ToArray();
                    Console.WriteLine($"接收数据{clientSocket.RemoteEndPoint}:{strees}");
                    
                   

                    Parallel.ForEach(Msgs, (Msg, state) =>
                    {
                        Console.WriteLine($"解析数据:{Msg}"); 
                        SocketMessage socketMessage = JsonSerializer.Deserialize<SocketMessage>(Msg); 
                        MessageReceived(socketMessage); 
                    });

                    StartReceiving(clientSocket);
                }
                else
                {
                    Disconnect(); // 客户端断开连接
                }
            }
            catch
            {

                Console.WriteLine($"与服务器连接中断");
                Disconnect(); // 客户端断开连接
            }

        }

        private async void HeartJump(Socket clientSocket)
        {
            //try
            //{
            //    Task.Factory.StartNew(async () => Socketlife(clientSocket));
            //    while (true)
            //    {
            //        await Task.Delay(5000);
            //        SocketMessage Msgjump = new SocketMessage();
            //        Msgjump.MesageType = 10000;
            //        if (!clientSocket.Connected)
            //        {
            //            return;
            //        }
            //        SendMessageToClient(clientSocket, JsonSerializer.Serialize(Msgjump) + "\n");
            //    }
            //}
            //catch
            //{
            //}
        }


        public void Disconnect()
        {
            _clientSocket.Close();
        } 
    }
}
