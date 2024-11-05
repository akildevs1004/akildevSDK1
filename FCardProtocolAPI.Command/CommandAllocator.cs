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
using FCardProtocolAPI.Command.Allocator;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using FluentScheduler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;

namespace FCardProtocolAPI.Command
{
    public class CommandAllocator
    {
        /// <summary>
        /// 连接分配器
        /// </summary>
        public static readonly ConnectorAllocator Allocator = ConnectorAllocator.GetAllocator();

        /// <summary>
        /// 设备类型校验类
        /// </summary>
        public static DeviceTypeFunc TypeFunc { get; set; } = new DeviceTypeFunc();
        /// <summary>
        /// 已注册的设备信息
        /// </summary>
        public static ConcurrentDictionary<string, DevicesInfo> DevicesInfos;
        /// <summary>
        /// websocket操作类
        /// </summary>
        public static WebSocketFunc SocketFunc { get; set; } = new WebSocketFunc();
        /// <summary>
        /// 服务器基本参数配置
        /// </summary>
        public static ServerOptions Options { get; set; } = new ServerOptions();
        /// <summary>
        /// 设备连接的客户端列表
        /// </summary>

        public static ConcurrentDictionary<string, MyConnectorDetail> ClientList = new ConcurrentDictionary<string, MyConnectorDetail>();

        public static CommandDetailFunc commandDetailFunc { get; set; } = new CommandDetailFunc();
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public async static Task Init(IConfiguration configuration)
        {
            BindOptions(configuration);

            AddEvent();

            #region Udp服务器绑定
            await BindUDPServer();
            #endregion

            #region tcp服务器绑定
            await BindTCPServer();
            #endregion
            JobManager.Initialize(new Jobs.MyRegistry(configuration));
        }
        /// <summary>
        /// 绑定配置项
        /// </summary>
        /// <param name="configuration"></param>
        private static void BindOptions(IConfiguration configuration)
        {
            configuration.GetSection("EquipmentType").Bind(TypeFunc.DeviceType);
            var devicesInfoList = new List<DevicesInfo>();
            configuration.GetSection("DevicesInfos").Bind(devicesInfoList);
            DevicesInfos = new ConcurrentDictionary<string, DevicesInfo>(devicesInfoList.ToDictionary(a => a.SN, a => a));
            configuration.GetSection("Options").Bind(Options);
        }

        /// <summary>
        /// tcp服务器绑定
        /// </summary>
        private async static Task BindTCPServer()
        {
            var tcp = new TCPServerDetail(Options.LocalIP, Options.TCPServerPort);
            await Allocator.OpenConnectorAsync(tcp);
            Console.WriteLine("绑定TCP服务器:" + Options.TCPServerPort);
        }
        /// <summary>
        /// 绑定UDP服务器
        /// </summary>
        /// <returns></returns>
        private async static Task BindUDPServer()
        {
            var udpDetail = new UDPServerDetail(Options.LocalIP, Options.UDPServerPort);
            await Allocator.OpenConnectorAsync(udpDetail);
            //   udpinc.SetKeepAliveOption(true, 30, new byte[0]);
            Console.WriteLine("绑定UDP服务器:" + Options.UDPServerPort);
        }
        /// <summary>
        /// 添加事件处理
        /// </summary>
        private static void AddEvent()
        {
            Allocator.TransactionMessage += MAllocator_TransactionMessage;
            Allocator.ClientOnline += MAllocator_ClientOnline;
            Allocator.ClientOffline += MAllocator_ClientOffline;
            Allocator.ConnectorClosedEvent += Allocator_ConnectorClosedEvent;
            Allocator.ConnectorErrorEvent += Allocator_ConnectorErrorEvent;
        }
        /// <summary>
        /// 连接错误事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connector"></param>
        private static void Allocator_ConnectorErrorEvent(object sender, INConnectorDetail connector)
        {
            Console.WriteLine("连接出错:" + connector.ToString());
        }
        /// <summary>
        /// 连接关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connector"></param>
        private static void Allocator_ConnectorClosedEvent(object sender, INConnectorDetail connector)
        {
            Console.WriteLine("连接关闭:" + connector.ToString());
        }
        /// <summary>
        /// 设备下线事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// <summary>
        /// 设备上线事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                if (TypeFunc.IsDoor8900H(sn))
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
        /// <summary>
        /// 数据监控数据处理事件
        /// </summary>
        /// <param name="connector"></param>
        /// <param name="EventData"></param>
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
                switch (fcTrn.CmdIndex)
                {
                    case 0x03:
                        SocketFunc.FaceSystemTransaction(fcTrn);
                        break;
                    case 0x22:
                        SocketFunc.KeepAliveTransaction(fcTrn);
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
        /// <summary>
        /// 测试连接响应
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="connector"></param>
        private static void SendConnectTest(string sn, INConnectorDetail connector)
        {
            var sndConntmsg = new SendConnectTestResponse(CommandDetailFunc.GetCommandDetail(sn, connector));
            Allocator.AddCommand(sndConntmsg);
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
                    OpenWatch(sn, dtl);
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
            var cmdDtl = CommandDetailFunc.GetCommandDetail(myConnectorDetail, sn);
            INCommand cmd;
            if (TypeFunc.IsDoor8900H(sn))
            {
                cmd = new DoNetDrive.Protocol.Door.Door8800.SystemParameter.Watch.CloseWatch(cmdDtl);
            }
            else
            {
                cmd = new DoNetDrive.Protocol.Fingerprint.SystemParameter.Watch.CloseWatch(cmdDtl);
            }
            Allocator.AddCommand(cmd);
        }

        private static void OpenWatch(string sn, MyConnectorDetail myConnectorDetail)
        {
            var cmdDtl = CommandDetailFunc.GetCommandDetail(myConnectorDetail, sn);
            INCommand cmd;
            if (TypeFunc.IsDoor8900H(sn))
            {
                cmd = new DoNetDrive.Protocol.Door.Door8800.SystemParameter.Watch.BeginWatch(cmdDtl);
            }
            else
            {
                cmd = new DoNetDrive.Protocol.Fingerprint.SystemParameter.Watch.BeginWatch(cmdDtl);
            }
            Allocator.AddCommand(cmd);
        }
    }
}
