using DoNetDrive.Common.Extensions;
using DoNetDrive.Core;
using DoNetDrive.Core.Command;
using DoNetDrive.Core.Connector;
using DoNetDrive.Core.Connector.TCPClient;
using DoNetDrive.Core.Connector.TCPServer;
using DoNetDrive.Core.Connector.UDP;
using DoNetDrive.Core.Data;
using DoNetDrive.Protocol;
using DoNetDrive.Protocol.Door8800;
using DoNetDrive.Protocol.Fingerprint.AdditionalData;
using DoNetDrive.Protocol.Fingerprint.Data.Transaction;
using DoNetDrive.Protocol.Fingerprint.SystemParameter;
using DoNetDrive.Protocol.Fingerprint.Transaction;
using DoNetDrive.Protocol.OnlineAccess;
using DoNetDrive.Protocol.Transaction;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using FluentScheduler;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace FCardProtocolAPI.Command
{
    public class CommandAllocator
    {
        public static readonly ConnectorAllocator Allocator = ConnectorAllocator.GetAllocator();
        public static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        /// <summary>
        /// 设备类型 门禁控制板或人脸指纹机
        /// </summary>
        public static Dictionary<string, string> DeviceType { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 已注册的设备信息
        /// </summary>
        public static ConcurrentDictionary<string, DevicesInfo> DevicesInfos;
        /// <summary>
        /// 已连接的websocket列表
        /// </summary>
        public static ConcurrentDictionary<string, WebSocket> WebSockets;

        public static ConcurrentDictionary<string, MyConnectorDetail> ClientList = new ConcurrentDictionary<string, MyConnectorDetail>();
        /// <summary>
        /// 设备通讯密码
        /// </summary>
        public static string ConnectionPassword { get; set; }
        /// <summary>
        /// 本地IP
        /// </summary>
        public static string LocalIP { get; set; }
        /// <summary>
        /// udp服务器端口
        /// </summary>
        public static int UDPServerPort { get; set; }
        /// <summary>
        /// tcp服务器端口
        /// </summary>
        public static int TCPServerPort { get; set; }
        /// <summary>
        /// 设备UDP端口
        /// </summary>
        public static int UDPPort { get; set; }
        /// <summary>
        /// 设备TCP端口
        /// </summary>
        public static int TCPPort { get; set; }
        private static int Timeout { get; set; }
        private static int RestartCount { get; set; }

        public static string Door8900HName = nameof(IDoor8900HCommand);
        public static string FingerprintCommandName = nameof(IFingerprintCommand);


        public async static Task Init(IConfiguration configuration)
        {
            //  ServiceProvider = provider;
            WebSockets = new ConcurrentDictionary<string, WebSocket>();
            configuration.GetSection("EquipmentType").Bind(DeviceType);
            var devicesInfoList = new List<DevicesInfo>();
            configuration.GetSection("DevicesInfos").Bind(devicesInfoList);
            DevicesInfos = new ConcurrentDictionary<string, DevicesInfo>(devicesInfoList.ToDictionary(a => a.SN, a => a));
            LocalIP = configuration["LocalIP"];
            // LoadLocalIP();
            UDPServerPort = int.Parse(configuration["UDPServerPort"]);
            TCPServerPort = int.Parse(configuration["TCPServerPort"]);
            UDPPort = int.Parse(configuration["UDPPort"]);
            TCPPort = int.Parse(configuration["TCPPort"]);
            Timeout = int.Parse(configuration["Timeout"]);
            RestartCount = int.Parse(configuration["RestartCount"]);
            ConnectionPassword = configuration["ConnectionPassword"];

            Allocator.TransactionMessage += MAllocator_TransactionMessage;
            Allocator.ClientOnline += MAllocator_ClientOnline;
            Allocator.ClientOffline += MAllocator_ClientOffline;
            Allocator.ConnectorClosedEvent += Allocator_ConnectorClosedEvent;
            Allocator.ConnectorErrorEvent += Allocator_ConnectorErrorEvent;

            #region Udp服务器绑定
            UDPServerDetail udpDetail = new UDPServerDetail(LocalIP, UDPServerPort);
            var udpinc = await Allocator.OpenConnectorAsync(udpDetail);
            //   udpinc.SetKeepAliveOption(true, 30, new byte[0]);
            Console.WriteLine("绑定UDP服务器:" + UDPServerPort);
            #endregion

            #region tcp服务器绑定

            var tcp = new TCPServerDetail(LocalIP, TCPServerPort);
            var tcpinc = await Allocator.OpenConnectorAsync(tcp);
            //tcpinc.SetKeepAliveOption(true, 30, new byte[0]);
            Console.WriteLine("绑定TCP服务器:" + TCPServerPort);
            JobManager.Initialize(new Jobs.MyRegistry());
            #endregion
        }

        private static void Allocator_ConnectorErrorEvent(object sender, INConnectorDetail connector)
        {
            Console.WriteLine("连接出错:" + connector.ToString());
        }

        private static void Allocator_ConnectorClosedEvent(object sender, INConnectorDetail connector)
        {
            Console.WriteLine("连接关闭:" + connector.ToString());
        }
        public static INCommandDetail GetCommandDetail(string sn, object userData = null)
        {
            INCommandDetail commandDetail = null;
            if (ClientList.ContainsKey(sn))
            {
                var tcp = ClientList[sn];
                commandDetail = GetCommandDetail(tcp, sn);
            }
            else if (DevicesInfos.ContainsKey(sn))
            {
                var detail = DevicesInfos[sn];
                var connectType = CommandDetailFactory.ConnectType.UDPClient;
                if (GetDeviceTypeName(sn).Equals(Door8900HName))
                {
                    connectType = CommandDetailFactory.ConnectType.TCPClient;
                }
                commandDetail = GetCommandDetail(connectType, detail.SN, detail.IP, detail.Port);
            }
            if (commandDetail != null)
                commandDetail.UserData = userData;
            return commandDetail;
        }

        public static INCommandDetail GetCommandDetail(string sn, INConnectorDetail connector)
        {

            var cmdDtl = new OnlineAccessCommandDetail(connector, sn, ConnectionPassword);
            cmdDtl.Timeout = Timeout;
            cmdDtl.RestartCount = RestartCount;
            return cmdDtl;
        }
        public static string GetDeviceTypeName(string sn)
        {
            var key = sn.Substring(0, 8);
            if (DeviceType.ContainsKey(key))
            {
                return DeviceType[key];
            }
            return string.Empty;
        }
        private static INCommandDetail GetCommandDetail(MyConnectorDetail clientDetail, string sn)
        {
            if (clientDetail.IsTCP)
            {
                var connectType = CommandDetailFactory.ConnectType.TCPServerClient;
                return GetCommandDetail(connectType, sn, clientDetail.ConnectorDetail.GetKey(), 0);
            }
            else
            {
                var connectType = CommandDetailFactory.ConnectType.UDPClient;
                var udpDetali = (UDPClientDetail)clientDetail.ConnectorDetail;
                return GetCommandDetail(connectType, sn, udpDetali.Addr, udpDetali.Port);
            }
        }
        /// <summary>
        /// 获取连接对接
        /// </summary>
        /// <param name="connectType"></param>
        /// <param name="sn"></param>
        /// <param name="addr"></param>
        /// <param name="udpPort"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private static INCommandDetail GetCommandDetail(CommandDetailFactory.ConnectType connectType, string sn, string addr, int udpPort)
        {
            //   var password = "FFFFFFFF";
            var protocolType = CommandDetailFactory.ControllerType.A33_Face;
            var cmdDtl = CommandDetailFactory.CreateDetail(connectType, addr, udpPort,
                protocolType, sn, ConnectionPassword);

            if (connectType == CommandDetailFactory.ConnectType.UDPClient)
            {
                var dtl = cmdDtl.Connector as TCPClientDetail;
                dtl.LocalAddr = LocalIP;
                dtl.LocalPort = UDPServerPort;
            }
            cmdDtl.Timeout = Timeout;
            cmdDtl.RestartCount = RestartCount;
            return cmdDtl;
        }
        private static void MAllocator_ClientOffline(object sender, ServerEventArgs e)
        {
            var inc = sender as INConnector;
            inc.RemoveRequestHandle(typeof(ConnectorObserverHandler));
            inc.RemoveRequestHandle(typeof(Door8800RequestHandle));
            var connectorType = inc.GetConnectorType();
            var key = inc.GetKey();
            if (connectorType == ConnectorType.TCPServerClient || connectorType == ConnectorType.UDPClient)
            {
                var keys = ClientList.Keys;
                foreach (var item in keys)
                {
                    if (key == ClientList[item].ConnectorDetail.GetKey())
                    {
                        if ((DateTime.Now - ClientList[item].KeepAliveTime).TotalMinutes < 1)
                            ClientList.Remove(item, out _);
                        break;
                    }
                }
            }
            Console.WriteLine(inc.GetConnectorType() + ":客户端离线");

        }

        private static void MAllocator_ClientOnline(object sender, ServerEventArgs e)
        {
            var inc = sender as INConnector;
            var connectorType = inc.GetConnectorType();
            Console.WriteLine(inc.RemoteAddress().ToString() + $":{connectorType}客户端上线");
            var key = inc.GetKey();
            switch (connectorType)
            {
                case ConnectorType.TCPServerClient://tcp 客户端已连接
                case ConnectorType.UDPClient://UDP客户端已连接
                    var fC8800Request = new Door8800RequestHandle(DotNetty.Buffers.UnpooledByteBufferAllocator.Default, RequestHandleFactory);
                    inc.RemoveRequestHandle(typeof(Door8800RequestHandle));//先删除，防止已存在就无法添加。
                    inc.AddRequestHandle(fC8800Request);
                    if (connectorType == ConnectorType.TCPServerClient)
                        inc.SetKeepAliveOption(true, 30, new byte[1]);
                    break;
                default:
                    break;
            }
        }
        /// <summary>
        /// 解析器处理工厂
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="cmdIndex"></param>
        /// <param name="cmdPar"></param>
        /// <returns></returns>
        private static AbstractTransaction RequestHandleFactory(string sn, byte cmdIndex, byte cmdPar)
        {
            if (cmdIndex >= 1 && cmdIndex <= 4)
            {
                if (GetDeviceTypeName(sn).Equals(Door8900HName))
                {
                    return DoNetDrive.Protocol.Door.Door89H.Transaction.ReadTransactionDatabaseByIndex.NewTransactionTable[cmdIndex]();
                }
                else
                {
                    return ReadTransactionDatabaseByIndex.NewTransactionTable[cmdIndex]();
                }
            }
            if (cmdIndex >= 5 && cmdIndex <= 6)
            {
                return DoNetDrive.Protocol.Door.Door89H.Transaction.ReadTransactionDatabaseByIndex.NewTransactionTable[cmdIndex]();
            }
            if (cmdIndex == 0x22)
            {
                return new DoNetDrive.Protocol.Door.Door8800.Data.Transaction.KeepaliveTransaction();
            }

            if (cmdIndex == 0xA0)
            {
                return new DoNetDrive.Protocol.Door.Door8800.Data.Transaction.ConnectMessageTransaction();
            }
            return null;
        }

        private static void MAllocator_TransactionMessage(INConnectorDetail connector, INData EventData)
        {
            try
            {

                var conn = Allocator.GetConnector(connector);
                if (conn != null)
                {
                    if (!conn.IsForciblyConnect())
                    {
                        conn.OpenForciblyConnect();
                    }
                }
                Door8800Transaction fcTrn = EventData as Door8800Transaction;
                AddClient(conn, fcTrn.SN);
              //  Console.WriteLine($"收到消息：0x{fcTrn.CmdIndex:x2}");
                switch (fcTrn.CmdIndex)
                {
                    //case 0x01:
                    //case 0x04:
                    //    if (WebSockets.Count > 0)
                    //        CardTransaction(fcTrn, connector);
                    //    break;
                    //case 0x02:
                    //case 0x03:
                    //case 0x05:
                    //case 0x06:
                    //    if (WebSockets.Count > 0)
                    //        DefaultTransaction(fcTrn);
                    //    break;
                    case 0x22:
                        if (WebSockets.Count > 0)
                            KeepAliveTransaction(fcTrn);
                        SendConnectTest(fcTrn.SN, connector);
                        break;
                    case 0xA0:
                        SendConnectTest(fcTrn.SN, connector);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("处理推送数据异常", ex);
            }
        }

        private static void SendConnectTest(string sn, INConnectorDetail connector)
        {
            var sndConntmsg = new SendConnectTestResponse(GetCommandDetail(sn, connector));
            Allocator.AddCommand(sndConntmsg);
        }


        public static IFcardCommandResult GetResult(Command.CommandStatus status, TransactionType type, object data = null)
        {
            var result = new FcardCommandResult();
            result.Message = "记录消息";
            result.Status = status;
            result.Data = data;
            result.TransactionType = type;
            return result;
        }
      

        private static void KeepAliveTransaction(Door8800Transaction transaction)
        {
            var record = new KeepAliveTransaction
            {
                SN = transaction.SN,
                KeepAliveTime = transaction.EventData.TransactionDate
            };
            SendWebSocketMessage(GetResult(CommandStatus.Succeed, TransactionType.KeepAliveTransaction, record));
        }
        private static void SendWebSocketMessage(IFcardCommandResult result)
        {
            var message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result, Settings));
            var buf = new ArraySegment<byte>(message);
            foreach (var item in WebSockets)
            {
                item.Value.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);
            }
           
        }
        /// <summary>
        /// 添加连接客户端
        /// </summary>
        /// <param name="inc"></param>
        /// <param name="sn"></param>
        private static void AddClient(INConnector inc, string sn)
        {
            var type = inc.GetConnectorType();
            if (type == ConnectorType.TCPServerClient || type == ConnectorType.UDPClient)
            {
                if (!ClientList.ContainsKey(sn))
                {
                    ClientList.TryAdd(sn, new MyConnectorDetail());
                }
                var dtl = ClientList[sn];
                dtl.IsClient = true;
                dtl.ConnectorDetail = inc.GetConnectorDetail();
                dtl.IsTCP = type == ConnectorType.TCPServerClient;
                dtl.SN = sn;
                if ((DateTime.Now - dtl.KeepAliveTime).TotalMinutes > 1.5)
                {
                    CloseWatch(sn, dtl);
                }
                dtl.KeepAliveTime = DateTime.Now;
            }
        }
        /// <summary>
        /// 开启监控
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="myConnectorDetail"></param>
        private static void CloseWatch(string sn, MyConnectorDetail myConnectorDetail)
        {
            var cmdDtl = GetCommandDetail(myConnectorDetail, sn);
            var commandName = GetDeviceType(sn);
            INCommand cmd;
            if (commandName.Equals(Door8900HName))
            {
                cmd = new DoNetDrive.Protocol.Door.Door8800.SystemParameter.Watch.CloseWatch(cmdDtl);
            }
            else
            {
                cmd = new DoNetDrive.Protocol.Fingerprint.SystemParameter.Watch.CloseWatch(cmdDtl);
            }
            Allocator.AddCommand(cmd);
        }
       
        public static string GetDeviceType(string sn)
        {
            var key = sn.Substring(0, 8);
            if (DeviceType.ContainsKey(key))
            {
                return DeviceType[key];
            }
            return string.Empty;
        }
    }
}
