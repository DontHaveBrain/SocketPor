using ISocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SocketPor
{
    [Export(typeof(ISocketble))]
    public class SocketServer : ISocketble
    {
        private string IPAddress = "0.0.0.0";
        private int Port = 8888;
        //接收消息事件
        public event Action<SocketMessage> MessageReceivedForServer;
        public event Action<SocketMessage, Socket> MessageReceivedForClient;

        //负责监听的Socket
        Socket SocketForWatch = null;
        //客户端池
        public ConcurrentDictionary<Socket, CancellationTokenSource> _clients = new ConcurrentDictionary<Socket, CancellationTokenSource>();
        //客户端信息缓存（string）
        ConcurrentDictionary<Socket, string> Messages = new ConcurrentDictionary<Socket, string>(); 
        //存储单个客户端对应发送缓存区
        ConcurrentDictionary<Socket, Queue<byte[]>> _byteForSendClien = new ConcurrentDictionary<Socket, Queue<byte[]>>();
        //存储单个客户端对应接收缓存区
        ConcurrentDictionary<Socket, Queue<byte[]>> _byteForReciveClien = new ConcurrentDictionary<Socket, Queue<byte[]>>();
        //存储用户心跳
        ConcurrentDictionary<Socket, int> Heartbeat = new ConcurrentDictionary<Socket, int>();
        //异步线程同步
        private AutoResetEvent _connectionWaitHandle = new AutoResetEvent(false);
        /// <说明>
        /// 导入参数构造服务器IPv4 
        /// </说明>
        /// <param name="ipAddress">IP地址 若赋值null则默认地址为0.0.0.0</param>
        /// <param name="port">端口号 默认值为8888</param>

        public SocketServer()
        {
        }
        public void IpConfig(string IPAddress, int port)
        {
            if (IPAddress != null)
            {
                this.IPAddress = IPAddress;
            }
            this.Port = port;
        }
        //初始绑定
        public bool InitialServerConnect()
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

                _clients.TryAdd(clientSocket, new CancellationTokenSource());
                _byteForSendClien.TryAdd(clientSocket, new Queue<byte[]>());
                _byteForReciveClien.TryAdd(clientSocket, new Queue<byte[]>());
                Heartbeat.TryAdd(clientSocket, 15); 
                Messages.TryAdd(clientSocket, "");
                Console.WriteLine($"客户端 {clientSocket.RemoteEndPoint} 连接成功");
                Task.Factory.StartNew(() => { SendMsgForOnlyClientAsync(clientSocket); });
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
        private async void StartReceiving(Socket clientSocket)
        {
            try
            {
                byte[] bufferForRecive=new byte[1024];
                Task.Factory.StartNew(() => { ReciveQueueProcess(clientSocket); });
                while (!_clients[clientSocket].IsCancellationRequested)
                {
                    try
                    {
                        int n = await clientSocket.ReceiveAsync(bufferForRecive);
                        _byteForReciveClien[clientSocket].Enqueue(bufferForRecive.Take(n).ToArray());
                        if (Heartbeat.ContainsKey(clientSocket))
                        {
                            Heartbeat[clientSocket] = 10;
                        }
                    }
                    catch{ }
                }
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
                if (_clients.ContainsKey(clientSocket))
                {
                    _clients[clientSocket].Cancel();
                    await Task.Delay(300);
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync("关断连接:" + ex.Message);
            }
            finally
            {
                _clients.TryRemove(clientSocket, out _); // 从字典中移除断开连接的客户端 
                Messages.TryRemove(clientSocket, out _); 
                Heartbeat.TryRemove(clientSocket, out _);
                _byteForSendClien.TryRemove(clientSocket, out _);
                _byteForReciveClien.TryRemove(clientSocket, out _);
                await Console.Out.WriteLineAsync("关断连接!!!!!!!!!!!!!!!");
            }
        }


        private async void ReciveQueueProcess(Socket clientSocket)
        { 
            try
            {
                while (_clients.ContainsKey(clientSocket) &&!_clients[clientSocket].IsCancellationRequested)
                {
                    if (_byteForReciveClien[clientSocket].TryDequeue(out byte[]? _buffer) && _buffer != null && _buffer.Length > 0)
                    {
                        try
                        {
                            Messages[clientSocket] += Encoding.UTF8.GetString(_buffer, 0, _buffer.Length);
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
                            Messages[clientSocket] = "";
                            Parallel.ForEach(Msgs, (Msg, state) =>
                            {
                                SocketMessage socketMessage;
                                try
                                {
                                    socketMessage = JsonSerializer.Deserialize<SocketMessage>(Msg);
                                }
                                catch
                                {
                                    return;
                                } 
                                if (socketMessage.MesageType != 10000)
                                {
                                    MessageReceivedForClient?.Invoke(socketMessage, clientSocket);
                                }
                            });
                        }
                        catch
                        {
                            Messages[clientSocket] = "";
                        }
                    }
                    else
                    {
                        await Task.Delay(25);
                    }
                }
            }
            catch (Exception ex)
            { 
                Console.WriteLine("服务器解析数据异常，现清空接收缓存---" + ex.Message);
                Messages[clientSocket] = "";
            }
        }



        public async Task SendMessageToClient(Socket clientSocket, SocketMessage message)
        {
            try
            {
                try
                {
                    string Message = JsonSerializer.Serialize(message) + "\n";
                    byte[] buffer = Encoding.UTF8.GetBytes(Message);
                    SendAsync(clientSocket, buffer);
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


        private async Task SendAsync(Socket socket, byte[] buffer)
        {
            await Task.Factory.StartNew(async () =>
             {
                 if (_byteForSendClien.Keys.Contains(socket))
                 {
                     _byteForSendClien[socket].Enqueue(buffer);
                 }

             });
        }
        private async Task SendMsgForOnlyClientAsync(Socket clientSocket)
        {
            try
            {
                Queue<byte[]> queue = null;
                if (_byteForSendClien.ContainsKey(clientSocket))
                {
                    try
                    {
                        queue = _byteForSendClien[clientSocket];
                    }
                    catch (Exception ex)
                    {
                        await Console.Out.WriteLineAsync(ex.Message);
                    }
                }
                else
                {
                    return;
                }
                while (_clients.Keys.Contains(clientSocket) && !_clients[clientSocket].IsCancellationRequested)
                {
                    if (queue.TryDequeue(out byte[]? messageInfo) && messageInfo != null)
                    {
                        try
                        {
                            await clientSocket.SendAsync(messageInfo);
                            await Task.Delay(30);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"给客户端发送消息错误: {ex.Message}");
                            Console.WriteLine($"清空发送队列");
                            queue.Clear();
                        }
                    }
                    else
                    {
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync("发送消息队列异常，现断开客户端连接" + ex.Message);
                DisconnectClient(clientSocket);
            }
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

                while (_clients.Keys.Contains(clientSocket) && !_clients[clientSocket].IsCancellationRequested)
                {
                    await Task.Delay(3000);
                    SocketMessage Msgjump = new SocketMessage();
                    Msgjump.MesageType = 10000;

                    try
                    {
                        if (!clientSocket.Connected)
                        {
                            return;
                        }
                        SendMessageToClient(clientSocket, Msgjump);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("心跳发送未成功将重发 错误原因：" + ex.Message);
                        SendMessageToClient(clientSocket, Msgjump);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("心跳发送异常 错误原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 接收心跳
        /// </summary>

        private async void Socketlife(Socket sk)
        {
            while (true)
            {
                await Task.Delay(1000);

                // 尝试获取并减少心跳计数，仅当当前计数大于0时进行更新 
                if (Heartbeat.ContainsKey(sk) && Heartbeat.TryGetValue(sk, out int currentCount) && currentCount > 0)
                {
                    Heartbeat[sk]--;
                    //Console.WriteLine($"客户端{socketName}在服务器的生命：" + (currentCount - 1));
                    // 如果更新后的心跳计数为0或负数，则断开连接并移除心跳记录 
                    if (currentCount - 1 <= 0)
                    {
                        await Console.Out.WriteLineAsync(sk.RemoteEndPoint.ToString() + "心跳停止，踢出队列");
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

        public async Task InitialClientConnectAsync()
        {
            Console.WriteLine("如需使用客户端请调用客户端封包");
        }

        public async Task SendMessageToServer(SocketMessage message)
        {
            Console.WriteLine("如需使用客户端请调用客户端封包");
        }

        public void Disconnect()
        {
            try
            {
                SocketForWatch.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
