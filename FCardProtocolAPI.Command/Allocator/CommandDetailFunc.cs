using DoNetDrive.Core.Command;
using DoNetDrive.Core.Connector.UDP;
using DoNetDrive.Core.Connector;
using DoNetDrive.Protocol.OnlineAccess;
using DoNetDrive.Protocol;
using FCardProtocolAPI.Command.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DoNetDrive.Core.Connector.TCPClient;

namespace FCardProtocolAPI.Command.Allocator
{
    public class CommandDetailFunc
    {
        public static INCommandDetail GetCommandDetail(string sn, object userData = null)
        {
            INCommandDetail commandDetail = null;
            if (CommandAllocator. ClientList.ContainsKey(sn))
            {
                var tcp = CommandAllocator.ClientList[sn];
                commandDetail = GetCommandDetail(tcp, sn);
            }
            else if (CommandAllocator.DevicesInfos.ContainsKey(sn))
            {
                var detail = CommandAllocator.DevicesInfos[sn];
                var connectType = CommandDetailFactory.ConnectType.UDPClient;
                if (CommandAllocator.TypeFunc.IsDoor8900H(sn))
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

            var cmdDtl = new OnlineAccessCommandDetail(connector, sn, CommandAllocator.Options.ConnectionPassword)
            {
                Timeout = CommandAllocator.Options.Timeout,
                RestartCount = CommandAllocator.Options.RestartCount
            };
            return cmdDtl;
        }

        public static INCommandDetail GetCommandDetail(MyConnectorDetail clientDetail, string sn)
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
        public static INCommandDetail GetCommandDetail(CommandDetailFactory.ConnectType connectType, string sn, string addr, int udpPort)
        {
            //   var password = "FFFFFFFF";
            var protocolType = CommandDetailFactory.ControllerType.A33_Face;
            var cmdDtl = CommandDetailFactory.CreateDetail(connectType, addr, udpPort,
                protocolType, sn, CommandAllocator.Options.ConnectionPassword);

            if (connectType == CommandDetailFactory.ConnectType.UDPClient)
            {
                var dtl = cmdDtl.Connector as TCPClientDetail;
                dtl.LocalAddr = CommandAllocator.Options.LocalIP;
                dtl.LocalPort = CommandAllocator.Options.UDPServerPort;
            }
            cmdDtl.Timeout = CommandAllocator.Options.Timeout;
            cmdDtl.RestartCount = CommandAllocator.Options.RestartCount;
            return cmdDtl;
        }
    }
}
