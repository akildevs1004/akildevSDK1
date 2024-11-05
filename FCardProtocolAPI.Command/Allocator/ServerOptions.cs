using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Allocator
{
    public class ServerOptions
    {
        /// <summary>
        /// 设备通讯密码
        /// </summary>
        public  string ConnectionPassword { get; set; }
        /// <summary>
        /// 本地IP
        /// </summary>
        public  string LocalIP { get; set; }
        /// <summary>
        /// udp服务器端口
        /// </summary>
        public  int UDPServerPort { get; set; }
        /// <summary>
        /// tcp服务器端口
        /// </summary>
        public  int TCPServerPort { get; set; }
        /// <summary>
        /// 设备UDP端口
        /// </summary>
        public  int UDPPort { get; set; }
        /// <summary>
        /// 设备TCP端口
        /// </summary>
        public  int TCPPort { get; set; }
        /// <summary>
        /// 超时时间
        /// </summary>
        public  int Timeout { get; set; }
        /// <summary>
        /// 命令重试次数
        /// </summary>
        public  int RestartCount { get; set; }
    }
}
