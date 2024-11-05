using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Allocator
{
    public class DeviceTypeFunc
    {
        /// <summary>
        /// 设备类型 门禁控制板或人脸指纹机
        /// </summary>
        public Dictionary<string, string> DeviceType { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 门禁设备接口名称
        /// </summary>
        private const string Door8900HName = nameof(IDoor8900HCommand);
        /// <summary>
        /// 人脸指纹设备接口名称
        /// </summary>
        private const string FingerprintCommandName = nameof(IFingerprintCommand);
        /// <summary>
        /// 获取设备对应的接口名称
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        private string GetDeviceTypeName(string sn)
        {
            var key = sn.Substring(0, 8);
            if (DeviceType.ContainsKey(key))
            {
                return DeviceType[key];
            }
            return string.Empty;
        }
        /// <summary>
        /// 判断是否是门禁设备
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        public bool IsDoor8900H(string sn)
        {
            return GetDeviceTypeName(sn).Equals(Door8900HName);
        }
        /// <summary>
        /// 检查设备是否存在配置文件中（如果不存在请到DeviceType.json中配置)
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        public bool CheckDeviceSn(string sn)
        {
            var commandName = GetDeviceTypeName(sn);          
            return commandName == Door8900HName || commandName == FingerprintCommandName;
        }
    }
}
