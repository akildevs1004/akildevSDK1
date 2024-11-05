using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Common
{
    public class MessageType
    {
        public static List<string[]> TransactionCodeNameList = new();

        public static List<string[]> CardTransactionCodeList = new List<string[]>();
        static MessageType()
        {
            if (TransactionCodeNameList.Count == 0)
            {
                var mCardTransactionList = new string[256];
                var mDoorSensorTransactionList = new string[256];
                var mSystemTransactionList = new string[256];
                TransactionCodeNameList.Add(null);//0是没有的
                TransactionCodeNameList.Add(mCardTransactionList);
                TransactionCodeNameList.Add(mDoorSensorTransactionList);
                TransactionCodeNameList.Add(mSystemTransactionList);
                mCardTransactionList[1] = "Swiping card verification";//
                mCardTransactionList[2] = "Fingerprint verification";//------------卡号为密码
                mCardTransactionList[3] = "Face verification";//
                mCardTransactionList[4] = "Fingerprint + Swiping card";//
                mCardTransactionList[5] = "Face + Fingerprint";//
                mCardTransactionList[6] = "Face + Swiping card";//   ---  常开工作方式中，刷卡进入常开状态
                mCardTransactionList[7] = "Swiping card + Password";//  --  多卡验证组合完毕后触发
                mCardTransactionList[8] = "Face + Password";//
                mCardTransactionList[9] = "Fingerprint + Password";//
                mCardTransactionList[10] = "Manually enter the user number and password for verification";//
                mCardTransactionList[11] = "Fingerprint+Swiping card+Password";//
                mCardTransactionList[12] = "Face+Swiping card+Password";//
                mCardTransactionList[13] = "Face+Fingerprint+Password";//  --  不开门
                mCardTransactionList[14] = "Face+Fingerprint+Swiping card";//
                mCardTransactionList[15] = "Repeated verification";//
                mCardTransactionList[16] = "Expiration date";//
                mCardTransactionList[17] = "Expiration of opening time zone";//------------卡号为错误密码
                mCardTransactionList[18] = "Can't open the door during holidays";//----卡号为卡号。
                mCardTransactionList[19] = "Unregistered user";//
                mCardTransactionList[20] = "Detection lock";//
                mCardTransactionList[21] = "The effective number has been used up";//
                mCardTransactionList[22] = "Verify when locked. Do not open the door ";//
                mCardTransactionList[23] = "Reported loss card ";//
                mCardTransactionList[24] = "Blacklist card";//
                mCardTransactionList[25] = "Open the door without verification -- the user number is 0 when pressing the fingerprint, and the user number is the card number when swiping the card";//
                mCardTransactionList[26] = "No card swiping verification -- when card swiping is disabled in 【access authentication mode】";//
                mCardTransactionList[27] = "No fingerprint verification when fingerprint is disabled in [access authentication mode]";//
                mCardTransactionList[28] = "Controller expired";//
                mCardTransactionList[29] = "Validation passed - expiration date is coming";//
                mCardTransactionList[30] = "Body temperature abnormal, refused to enter";//

                mDoorSensorTransactionList[1] = "Open the door";//
                mDoorSensorTransactionList[2] = "Close the door";//
                mDoorSensorTransactionList[3] = "Enter the door sensor alarm detection status";//
                mDoorSensorTransactionList[4] = "Exit door sensor alarm detection status";//
                mDoorSensorTransactionList[5] = "Door not close completely";//
                mDoorSensorTransactionList[6] = "Use the button to open the door";//
                mDoorSensorTransactionList[7] = "The door is locked when the button opens";//
                mDoorSensorTransactionList[8] = "The controller has expired when the button opens the door";//

                mSystemTransactionList[1] = "Software unlocked";//
                mSystemTransactionList[2] = "Software locked";//
                mSystemTransactionList[3] = "Software normally open";//
                mSystemTransactionList[4] = "Controller automatically enters into normally open";//
                mSystemTransactionList[5] = "Controller automatically close the door";//
                mSystemTransactionList[6] = "Long press door switch normally open";//
                mSystemTransactionList[7] = "Long press door switch normally close";//
                mSystemTransactionList[8] = "Software locked";//
                mSystemTransactionList[9] = "Software unlocked";//
                mSystemTransactionList[10] = "Controller timing locked--automatically locked on time";//
                mSystemTransactionList[11] = "Controller timing locked--automatically unlocked on time";//
                mSystemTransactionList[12] = "Alarm--Locked";//
                mSystemTransactionList[13] = "Alarm--Unlocked";//
                mSystemTransactionList[14] = "Illegal verification alarm";//
                mSystemTransactionList[15] = "Door sensor alarm";//
                mSystemTransactionList[16] = "Duress alarm";//
                mSystemTransactionList[17] = "Door timeout alarm";//
                mSystemTransactionList[18] = "Blacklist alarm";//
                mSystemTransactionList[19] = "Fire alarm";//
                mSystemTransactionList[20] = "Tamper alarm";//
                mSystemTransactionList[21] = "Remove illegal verification alarm";//
                mSystemTransactionList[22] = "Remove door sensor alarm";//
                mSystemTransactionList[23] = "Remove duress alarm";//
                mSystemTransactionList[24] = "Remove door timeout alarm";//
                mSystemTransactionList[25] = "Remove blacklist alarm";//
                mSystemTransactionList[26] = "Remove fire alarm";//
                mSystemTransactionList[27] = "Remove tamper alarm";//
                mSystemTransactionList[28] = "System is powered";//
                mSystemTransactionList[29] = "System error reset（watchdog）";//
                mSystemTransactionList[30] = "Device formatting records";//
                mSystemTransactionList[31] = "Card reader is connected backwards.";//
                mSystemTransactionList[32] = "Card reader is not connected properly.";//
                mSystemTransactionList[33] = "Unrecognized card reader";//
                mSystemTransactionList[34] = "Network cable is disconnected";//
                mSystemTransactionList[35] = "Network cable has been inserted";//
                mSystemTransactionList[36] = "WIFI is connected";//
                mSystemTransactionList[37] = "WIFI is disconnected";//
            }

          

            if (CardTransactionCodeList.Count == 0)
            {
             var   mCardTransactionList = new string[256];
                var mButtonTransactionList = new string[256];
                var mDoorSensorTransactionList = new string[256];
                var mSoftwareTransactionList = new string[256];
                var mAlarmTransactionList = new string[256];
                var mSystemTransactionList = new string[256];

                
                CardTransactionCodeList.Add(null);//0是没有的
                CardTransactionCodeList.Add(mCardTransactionList);
                CardTransactionCodeList.Add(mButtonTransactionList);
                CardTransactionCodeList.Add(mDoorSensorTransactionList);
                CardTransactionCodeList.Add(mSoftwareTransactionList);
                CardTransactionCodeList.Add(mAlarmTransactionList);
                CardTransactionCodeList.Add(mSystemTransactionList);

                mCardTransactionList[1] = "合法开门";//
                mCardTransactionList[2] = "密码开门";//------------卡号为密码
                mCardTransactionList[3] = "卡加密码";//
                mCardTransactionList[4] = "手动输入卡加密码";//
                mCardTransactionList[5] = "首卡开门";//
                mCardTransactionList[6] = "门常开";//   ---  常开工作方式中，刷卡进入常开状态
                mCardTransactionList[7] = "多卡开门";//  --  多卡验证组合完毕后触发
                mCardTransactionList[8] = "重复读卡";//
                mCardTransactionList[9] = "有效期过期";//
                mCardTransactionList[10] = "开门时段过期";//
                mCardTransactionList[11] = "节假日无效";//
                mCardTransactionList[12] = "未注册卡";//
                mCardTransactionList[13] = "巡更卡";//  --  不开门
                mCardTransactionList[14] = "探测锁定";//
                mCardTransactionList[15] = "无有效次数";//
                mCardTransactionList[16] = "防潜回";//
                mCardTransactionList[17] = "密码错误";//------------卡号为错误密码
                mCardTransactionList[18] = "密码加卡模式密码错误";//----卡号为卡号。
                mCardTransactionList[19] = "锁定时(读卡)或(读卡加密码)开门";//
                mCardTransactionList[20] = "锁定时(密码开门)";//
                mCardTransactionList[21] = "首卡未开门";//
                mCardTransactionList[22] = "挂失卡";//
                mCardTransactionList[23] = "黑名单卡";//
                mCardTransactionList[24] = "门内上限已满，禁止入门。";//
                mCardTransactionList[25] = "开启防盗布防状态(设置卡)";//
                mCardTransactionList[26] = "撤销防盗布防状态(设置卡)";//
                mCardTransactionList[27] = "开启防盗布防状态(密码)";//
                mCardTransactionList[28] = "撤销防盗布防状态(密码)";//
                mCardTransactionList[29] = "互锁时(读卡)或(读卡加密码)开门";//
                mCardTransactionList[30] = "互锁时(密码开门)";//
                mCardTransactionList[31] = "全卡开门";//
                mCardTransactionList[32] = "多卡开门--等待下张卡";//
                mCardTransactionList[33] = "多卡开门--组合错误";//
                mCardTransactionList[34] = "非首卡时段刷卡无效";//
                mCardTransactionList[35] = "非首卡时段密码无效";//
                mCardTransactionList[36] = "禁止刷卡开门";//  --  【开门认证方式】验证模式中禁用了刷卡开门时
                mCardTransactionList[37] = "禁止密码开门";//  --  【开门认证方式】验证模式中禁用了密码开门时
                mCardTransactionList[38] = "门内已刷卡，等待门外刷卡。";//（门内外刷卡验证）
                mCardTransactionList[39] = "门外已刷卡，等待门内刷卡。";//（门内外刷卡验证）
                mCardTransactionList[40] = "请刷管理卡";//(在开启管理卡功能后提示)(电梯板)
                mCardTransactionList[41] = "请刷普通卡";//(在开启管理卡功能后提示)(电梯板)
                mCardTransactionList[42] = "首卡未读卡时禁止密码开门。";//
                mCardTransactionList[43] = "控制器已过期_刷卡";//
                mCardTransactionList[44] = "控制器已过期_密码";//
                mCardTransactionList[45] = "合法卡开门—有效期即将过期";//
                mCardTransactionList[46] = "拒绝开门--区域反潜回失去主机连接。";//
                mCardTransactionList[47] = "拒绝开门--区域互锁，失去主机连接";//
                mCardTransactionList[48] = "区域防潜回--拒绝开门";//
                mCardTransactionList[49] = "区域互锁--有门未关好，拒绝开门";//                
                mCardTransactionList[50] = "开门密码有效次数过期";//
                mCardTransactionList[51] = "开门密码有效期过期";//
                mCardTransactionList[52] = "二维码已过期";//

                mButtonTransactionList[1] = "按钮开门";//
                mButtonTransactionList[2] = "开门时段过期";//
                mButtonTransactionList[3] = "锁定时按钮";//
                mButtonTransactionList[4] = "控制器已过期";//
                mButtonTransactionList[5] = "互锁时按钮(不开门)";//

                mDoorSensorTransactionList[1] = "开门";//
                mDoorSensorTransactionList[2] = "关门";//
                mDoorSensorTransactionList[3] = "进入门磁报警状态";//
                mDoorSensorTransactionList[4] = "退出门磁报警状态";//
                mDoorSensorTransactionList[5] = "门未关好";//

                mSoftwareTransactionList[1] = "软件开门";//
                mSoftwareTransactionList[2] = "软件关门";//
                mSoftwareTransactionList[3] = "软件常开";//
                mSoftwareTransactionList[4] = "控制器自动进入常开";//
                mSoftwareTransactionList[5] = "控制器自动关闭门";//
                mSoftwareTransactionList[6] = "长按出门按钮常开";//
                mSoftwareTransactionList[7] = "长按出门按钮常闭";//
                mSoftwareTransactionList[8] = "软件锁定";//
                mSoftwareTransactionList[9] = "软件解除锁定";//
                mSoftwareTransactionList[10] = "控制器定时锁定";//--到时间自动锁定
                mSoftwareTransactionList[11] = "控制器定时解除锁定";//--到时间自动解除锁定
                mSoftwareTransactionList[12] = "报警--锁定";//
                mSoftwareTransactionList[13] = "报警--解除锁定";//
                mSoftwareTransactionList[14] = "互锁时远程开门";//

                mAlarmTransactionList[1] = "门磁报警";//
                mAlarmTransactionList[2] = "匪警报警";//
                mAlarmTransactionList[3] = "消防报警";//
                mAlarmTransactionList[4] = "非法卡刷报警";//
                mAlarmTransactionList[5] = "胁迫报警";//
                mAlarmTransactionList[6] = "消防报警(命令通知)";//
                mAlarmTransactionList[7] = "烟雾报警";//
                mAlarmTransactionList[8] = "防盗报警";//
                mAlarmTransactionList[9] = "黑名单报警";//
                mAlarmTransactionList[10] = "开门超时报警";//
                mAlarmTransactionList[0x11] = "门磁报警撤销";//
                mAlarmTransactionList[0x12] = "匪警报警撤销";//
                mAlarmTransactionList[0x13] = "消防报警撤销";//
                mAlarmTransactionList[0x14] = "非法卡刷报警撤销";//
                mAlarmTransactionList[0x15] = "胁迫报警撤销";//
                mAlarmTransactionList[0x17] = "撤销烟雾报警";//
                mAlarmTransactionList[0x18] = "关闭防盗报警";//
                mAlarmTransactionList[0x19] = "关闭黑名单报警";//
                mAlarmTransactionList[0x1A] = "关闭开门超时报警";//
                mAlarmTransactionList[0x21] = "门磁报警撤销(命令通知)";//
                mAlarmTransactionList[0x22] = "匪警报警撤销(命令通知)";//
                mAlarmTransactionList[0x23] = "消防报警撤销(命令通知)";//
                mAlarmTransactionList[0x24] = "非法卡刷报警撤销(命令通知)";//
                mAlarmTransactionList[0x25] = "胁迫报警撤销(命令通知)";//
                mAlarmTransactionList[0x27] = "撤销烟雾报警(命令通知)";//
                mAlarmTransactionList[0x28] = "关闭防盗报警(软件关闭)";//
                mAlarmTransactionList[0x29] = "关闭黑名单报警(软件关闭)";//
                mAlarmTransactionList[0x2A] = "关闭开门超时报警";//

                mSystemTransactionList[1] = "系统加电";//
                mSystemTransactionList[2] = "系统错误复位（看门狗）";//
                mSystemTransactionList[3] = "设备格式化记录";//
                mSystemTransactionList[4] = "系统高温记录，温度大于>75";//
                mSystemTransactionList[5] = "系统UPS供电记录";//
                mSystemTransactionList[6] = "温度传感器损坏，温度大于>100";//
                mSystemTransactionList[7] = "电压过低，小于<09V";//
                mSystemTransactionList[8] = "电压过高，大于>14V";//
                mSystemTransactionList[9] = "读卡器接反。";//
                mSystemTransactionList[10] = "读卡器线路未接好。";//
                mSystemTransactionList[11] = "无法识别的读卡器";//
                mSystemTransactionList[12] = "电压恢复正常，小于14V，大于9V";//
                mSystemTransactionList[13] = "网线已断开";//
                mSystemTransactionList[14] = "网线已插入";//
            }

        }
    }
}
