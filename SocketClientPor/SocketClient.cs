using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using ISocket;
using System.ComponentModel.Composition;
using System.Collections;
using System.Collections.Concurrent;

namespace SocketClientPor
{
    [Export(typeof(ISocketble))]
    public class SocketClient : ISocketble
    {
        //用于交互的socket
        private Socket _clientSocket;
        //交互数据(byte)
        private byte[] _buffer = new byte[1024];
        //交互数据(string)
        private string MsgStore = string.Empty;
        //数据接收事件
        public event Action<SocketMessage> MessageReceivedForServer;
        public event Action<SocketMessage, Socket> MessageReceivedForClient;
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
        private string ipAddress = "0.0.0.0";
        private int port = 6421;
        bool ctsForSocket = true;
        public void IpConfig(string IPAddress, int port)
        {
            ipAddress = IPAddress;
            this.port = port;
        }
        public async Task InitialClientConnectAsync()
        {
            await Console.Out.WriteLineAsync("开始连接服务器");
            try
            {
                if (_clientSocket != null)
                {
                    _clientSocket.Close();
                }
            }
            catch { }


            try
            {
                IPAddress ipAddress = System.Net.IPAddress.Parse(this.ipAddress);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                _clientSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.Connect(remoteEP);
                await Console.Out.WriteLineAsync($"已连接到服务器 {this.ipAddress}:{this.port}");
                ctsForSocket = true;

                StartReceiving(_clientSocket);
            }
            catch (Exception ex)
            {
                ctsForSocket = false;
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
                await Console.Out.WriteLineAsync($"无法连接到服务器，正在尝试重新连接..." + ex.Message);
                await Task.Delay(5000);
                InitialClientConnectAsync();
            }

        }

        public async Task SendMessageToServer(SocketMessage message)
        {
            try
            {
                string Message = JsonSerializer.Serialize(message) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(Message);
                await Task.Delay(1);
                Task.Run(send(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine("发生异常：{0}", ex.Message);
            }
        }
        object LOCKFORSOCKET = new object();
        private Func<Task?> send(byte[] data)
        {
            lock (LOCKFORSOCKET)
            {
                Thread.Sleep(20);
            }
            return async () => { _clientSocket.BeginSend(data, 0, data.Length, SocketFlags.None, null, _clientSocket); };
        }

        private void StartReceiving(Socket clientSocket)
        {
            try
            {
                Task.Factory.StartNew(() => { ReceiveByte(_clientSocket); });
                Task.Factory.StartNew(async () => HeartJump(_clientSocket));
                Task.Factory.StartNew(async () => Socketlife());
                Task.Factory.StartNew(() => ReceiveCallback());
                Task.Factory.StartNew(() => { PickTimeNum(); });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接收服务器 {clientSocket.RemoteEndPoint} ——数据异常，错误信息：{ex.Message}");
            }
        }
        Queue<byte[]> receiveByte = new Queue<byte[]>();
        private void ReceiveByte(Socket clientSocket)
        {
            byte[] data = new byte[1024];
            while (ctsForSocket)
            {
                try
                {
                    int n = clientSocket.Receive(data);
                    if (n==0)
                    {
                        Heartbeat = 0;
                    }
                    else
                    {
                        receiveByte.Enqueue(data.Take(n).ToArray());
                    } 
                }
                catch  (Exception ex)
                {
                    Console.WriteLine("数据存入队列异常"+ex.Message);
                    throw new Exception("数据存入队列异常");
                }
            }
        } 
        private async void ReceiveCallback()
        { 
            
            while (ctsForSocket)
            {
                try
                { 
                    if (receiveByte.TryDequeue(out byte[]? _buffer) && _buffer != null)
                    { 
                        MsgStore += Encoding.UTF8.GetString(_buffer, 0, _buffer.Length);

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
                        Parallel.ForEach(Msgs, (Action<string, ParallelLoopState>)((Msg, state) =>
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
                            Heartbeat = 10;
                            if (socketMessage.MesageType != 10000)
                            {
                                MessageReceivedForServer?.Invoke(socketMessage);
                            }
                        }));
                    }
                    else
                    {  
                        await Task.Delay(25);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("客户端解析数据异常" + ex.Message);
                    receiveByte.Clear();
                }

            }

        }
        private async void PickTimeNum()
        {

            while (ctsForSocket)
            {
                await Task.Delay(1000);
                if (Heartbeat==3)
                {
                    ClearReceiveBuffer(_clientSocket);
                } 
            }
        }
        private void ClearReceiveBuffer(Socket socket)
        {
            byte[] buffer = new byte[4096]; // 使用适当大小的缓冲区
            try
            {
                // 将Socket设置为非阻塞模式
                socket.Blocking = false;
                int receivedBytes = 0;
                do
                {
                    receivedBytes = socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                } while (receivedBytes > 0);


                Console.Out.WriteLineAsync("清空接收缓存区 完成");
            }
            catch (SocketException ex)
            { 
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                { 
                    Console.WriteLine($"Soket销毁缓存区错误: {ex.SocketErrorCode}");
                }
            }
            finally
            {
                // 恢复Socket的阻塞模式
                socket.Blocking = true;
                receiveByte.Clear();
            }
        }

        private volatile int Heartbeat = 10;
        private async void HeartJump(Socket clientSocket)
        {
            try
            { 
                SocketMessage Msgjump = new SocketMessage();
                Msgjump.MesageType = 10000;
                while (true)
                {
                    await Task.Delay(3000);

                    if (!clientSocket.Connected)
                    {
                        return;
                    }
                    SendMessageToServer(Msgjump);
                }
            }
            catch { }
        }
        private async void Socketlife()
        {
            Heartbeat = 10;
            while (true)
            {
                try
                {
                    await Task.Delay(1000);
                    Heartbeat--;
                    // 尝试获取并减少心跳计数，仅当当前计数大于0时进行更新 
                    if (Heartbeat > 0)
                    {
                        Console.WriteLine("客户端生命：" + Heartbeat);
                        // 如果更新后的心跳计数为0或负数，则断开连接并移除心跳记录  
                    }
                    else
                    {
                        await Console.Out.WriteLineAsync("心跳停止，断开连接");
                        Disconnect();
                        Task.Factory.StartNew(() => { InitialClientConnectAsync(); });
                        break;
                    }
                }
                catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync("心跳异常：" + ex.Message);
                    Disconnect();
                    Task.Factory.StartNew(() => { InitialClientConnectAsync(); });
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                receiveByte.Clear();
                _clientSocket.Close();
                MsgStore = "";
                ctsForSocket = false;
                receiveByte.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine("" + ex.Message);
            }
        }

        public bool InitialServerConnect()
        {
            Console.WriteLine("如需初始化服务器请调用服务器封包");
            return false;
        }

        public async Task SendMessageToClient(Socket clientSocket, SocketMessage message)
        {
            Console.WriteLine("如需使用服务器请调用服务器封包");
        }
    }
}
