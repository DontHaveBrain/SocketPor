using ISocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SocketPor
{
    public class SocketServer
    {
        private string IPAddress = "0.0.0.0";
        private int Port = 8888;
        //接收消息事件
        public event Action<SocketMessage, Socket> MessageReceived;
        //负责监听的Socket
        Socket SocketForWatch = null;
        //客户端池
        public ConcurrentDictionary<Socket, CancellationTokenSource> _clients = new ConcurrentDictionary<Socket, CancellationTokenSource>();
        //客户端信息缓存（string）
        ConcurrentDictionary<Socket, string> Messages = new ConcurrentDictionary<Socket, string>();
        //客户端信息缓存（byte）
        ConcurrentDictionary<Socket, byte[]> _byteForClien = new ConcurrentDictionary<Socket, byte[]>();
        //异步线程同步
        private AutoResetEvent _connectionWaitHandle = new AutoResetEvent(false);
        /// <说明>
        /// 导入参数构造服务器IPv4 
        /// </说明>
        /// <param name="ipAddress">IP地址 若赋值null则默认地址为0.0.0.0</param>
        /// <param name="port">端口号 默认值为8888</param>
        public SocketServer(string ipAddress, int port)
        {
            if (ipAddress != null)
            {
                IPAddress = ipAddress;
            }
            if (port != 8888)
            {
                Port = port;
            }
        }
        //初始绑定
        public bool Connecting()
        {
            try
            {
                //声明
                SocketForWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //ip转换
                IPAddress IPForBind = System.Net.IPAddress.Parse(IPAddress);
                //绑定
                SocketForWatch.Bind(new IPEndPoint(IPForBind, Port));
            }
            catch
            {
                Console.WriteLine($"服务器监听异常，监听地址：{IPAddress}:{Port}");
                return false;
            }

            //绑定成功开始监听
            Task.Factory.StartNew(async () => { StartListening(); });
            Thread.Sleep(50);
            return true;
        }
        private async Task StartListening()
        {
            try
            {
                SocketForWatch.Listen(1000); // 最大连接数
                Console.WriteLine($"服务器启动成功，监听地址：{IPAddress}:{Port}");

                while (true)
                {
                    _connectionWaitHandle.Reset();

                    //用户连接
                    SocketForWatch.BeginAccept(new AsyncCallback(AcceptCallback), null);

                    _connectionWaitHandle.WaitOne(); // 等待客户端连接完成才继续循环
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器启动失败，错误信息：{ex.Message}");
            }
        }
        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = SocketForWatch.EndAccept(ar);
                _clients[clientSocket] = null;
                //} // 添加客户端到字典中，值表示连接状态

                Console.WriteLine($"客户端 {clientSocket.RemoteEndPoint} 连接成功");
                Task.Factory.StartNew(async () => HeartJump(clientSocket));


                StartReceiving(clientSocket);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"客户端连接中断，错误代码：{ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理客户端连接时发生异常，错误信息：{ex.Message}");
            }
            finally
            {
                _connectionWaitHandle.Set(); // 通知主线程客户端连接已完成
            }
        }
        private void StartReceiving(Socket clientSocket)
        {
            try
            {
                if (!_byteForClien.Keys.Contains(clientSocket))
                {
                    _byteForClien.TryAdd(clientSocket, new byte[1024]);
                }
                clientSocket.BeginReceive(_byteForClien[clientSocket], 0, _byteForClien[clientSocket].Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), clientSocket);


            }
            catch (SocketException ex)
            {
                Console.WriteLine($"开始接收来自客户端 {clientSocket.RemoteEndPoint} 的数据失败，错误代码：{ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"开始接收来自客户端 {clientSocket.RemoteEndPoint} 的数据时发生异常，错误信息：{ex.Message}");
            }
        }
        private async void DisconnectClient(Socket clientSocket)
        {
            try
            {
                try
                {
                    clientSocket.Close();
                }
                catch { }
                if (_clients[clientSocket] != null)
                {
                    _clients[clientSocket].Cancel();
                    await Task.Delay(300);
                }
                _clients.TryRemove(clientSocket, out _); // 从字典中移除断开连接的客户端

                Messages.TryRemove(clientSocket, out _);
                _byteForClien.TryRemove(clientSocket, out _);
            }
            catch
            {
            }
            finally
            {
                _clients.TryRemove(clientSocket, out _); // 从字典中移除断开连接的客户端

                Messages.TryRemove(clientSocket, out _);
                _byteForClien.TryRemove(clientSocket, out _);
            }
        }


        private void ReceiveCallback(IAsyncResult ar)
        {
            Socket clientSocket = (Socket)ar.AsyncState;
            if (!Messages.Keys.Contains(clientSocket))
            {
                Messages.TryAdd(clientSocket, "");
            }

            try
            {
                int bytesRead = clientSocket.EndReceive(ar);

                if (bytesRead > 0)
                {
                    Messages[clientSocket] += Encoding.UTF8.GetString(_byteForClien[clientSocket], 0, bytesRead);
                    if (!Messages[clientSocket].StartsWith("{"))
                    {
                        Messages[clientSocket] = "";
                        return;
                    }
                    string strees = Messages[clientSocket];
                    if (!(Messages[clientSocket].EndsWith("}") || Messages[clientSocket].EndsWith("\r\n") || Messages[clientSocket].EndsWith("\r") || Messages[clientSocket].EndsWith("\n")))
                    {
                        return;
                    }
                    string[] Msgs = Messages[clientSocket].Split('\n').Where(s => !string.IsNullOrEmpty(s)).Where(x => (x.StartsWith('{') && x.EndsWith('}'))).ToArray();
                    Console.WriteLine($"接收数据{clientSocket.RemoteEndPoint}:{Messages[clientSocket]}");
                    Messages[clientSocket] = "";
                    // 继续接收客户端数据

                    Parallel.ForEach(Msgs, (Msg, state) =>
                    {
                        Console.WriteLine($"解析数据:{Msg}");
                       
                        #region MyRegion
                        SocketMessage socketMessage = JsonSerializer.Deserialize<SocketMessage>(Msg);
                        if (socketMessage.MesageType == 10000)
                        {
                            if (Heartbeat.ContainsKey(clientSocket))
                            {
                                Heartbeat[clientSocket] = 15;
                            }
                            
                        }
                        MessageReceived(socketMessage, clientSocket);
                        #endregion
                    });

                    StartReceiving(clientSocket);
                }
                else
                {
                    DisconnectClient(clientSocket); // 客户端断开连接
                }
            }
            catch
            {

                Console.WriteLine($"与客户端连接中断");
                DisconnectClient(clientSocket); // 客户端断开连接
            }

        }



        public async Task SendMessageToClient(Socket clientSocket, string message)
        {
            try
            {
                try
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    await SendAsync(clientSocket, buffer, 0, buffer.Length);
                    await Task.Delay(30);
                }
                catch
                {
                    DisconnectClient(clientSocket);
                }

            }
            catch (SocketException ex)
            {
                Console.WriteLine($"发送消息给客户端 {clientSocket.RemoteEndPoint} 失败，错误代码：{ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送消息给客户端 {clientSocket.RemoteEndPoint} 时发生异常，错误信息：{ex.Message}");

            }
        }

        private static async Task<int> SendAsync(Socket socket, byte[] buffer, int offset, int count)
        {
            return await Task.Factory.FromAsync(socket.BeginSend(buffer, offset, count, SocketFlags.None, null, socket), socket.EndSend);
        }


        /// <summary>
        /// 发送心跳
        /// </summary>
        /// <param name="clientSocket"></param>
        private async void HeartJump(Socket clientSocket)
        {
            try
            {
                Task.Factory.StartNew(async () => Socketlife(clientSocket));
                while (true)
                {
                    await Task.Delay(5000);
                    SocketMessage Msgjump = new SocketMessage();
                    Msgjump.MesageType = 10000;
                    if (!clientSocket.Connected)
                    {
                        return;
                    }
                    SendMessageToClient(clientSocket, JsonSerializer.Serialize(Msgjump) + "\n"); 
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 接收心跳
        /// </summary>
        ConcurrentDictionary<Socket, int> Heartbeat = new ConcurrentDictionary<Socket, int>();
        private async void Socketlife(Socket sk)
        {
            Heartbeat[sk] = 15;
            while (true)
            {
                await Task.Delay(1000);
          
                // 尝试获取并减少心跳计数，仅当当前计数大于0时进行更新 
                if (Heartbeat.TryGetValue(sk, out int currentCount) && currentCount > 0 && Heartbeat.TryUpdate(sk, currentCount - 1, currentCount)) 
                {
                    Console.WriteLine("客户端生命："+currentCount);
                    // 如果更新后的心跳计数为0或负数，则断开连接并移除心跳记录 
                    if (currentCount - 1 <= 0) 
                    { 
                        DisconnectClient(sk); 
                        Heartbeat.TryRemove(sk, out _); 
                        return; 
                    } 
                } 
                else 
                { 
                    break; 
                }
            }
        }
    }
}
