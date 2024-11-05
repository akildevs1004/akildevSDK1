using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Jobs
{
    public class RecordOptions
    {
        /// <summary>
        /// 启用人脸、读卡记录
        /// </summary>
        public bool CardTransaction { get; set; }
        /// <summary>
        /// 记录图片
        /// </summary>
        public bool RecordImage { get; set; }
        /// <summary>
        /// 体温记录
        /// </summary>
        public bool BodyTemperatureTransaction { get; set; }
        /// <summary>
        /// 系统记录
        /// </summary>
        public bool SystemTransaction { get; set; }
        /// <summary>
        /// 按钮记录
        /// </summary>
        public bool ButtonTransaction { get; set; }
        /// <summary>
        /// 门磁记录
        /// </summary>
        public bool DoorSensorTransaction { get; set; }
        /// <summary>
        /// 软件操作记录
        /// </summary>
        public bool SoftwareTransaction { get; set; }
        /// <summary>
        /// 报警记录
        /// </summary>
        public bool AlarmTransaction { get; set; }
        /// <summary>
        /// 检查是否需要推送
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool CheckFingerprint(int type)
        {
            return (type == 1 && CardTransaction) ||
                   (type == 2 && DoorSensorTransaction) ||
                   (type == 3 && SystemTransaction) ||
                   (type == 4 && BodyTemperatureTransaction);
        }
        /// <summary>
        /// 检查是否需要推送
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool CheckDoor(int type)
        {
            return (type == 1 && CardTransaction) ||
                   (type == 2 && ButtonTransaction) ||
                   (type == 3 && DoorSensorTransaction) ||
                   (type == 4 && SoftwareTransaction) ||
                   (type == 5 && SystemTransaction) ||
                   (type == 6 && AlarmTransaction);
        }
    }
}
