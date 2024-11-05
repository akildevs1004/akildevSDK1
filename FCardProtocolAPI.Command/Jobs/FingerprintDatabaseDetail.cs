using DoNetDrive.Common.Extensions;
using DoNetDrive.Core.Command;
using DoNetDrive.Protocol.Door.Door8800.Data;
using DoNetDrive.Protocol.Door.Door8800.Transaction;
using DoNetDrive.Protocol.Fingerprint.AdditionalData;
using DoNetDrive.Protocol.Fingerprint.Transaction;
using DoNetDrive.Protocol.Transaction;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Jobs
{
    /// <summary>
    /// 人脸数据详情
    /// </summary>
    public class FingerprintDatabaseDetail : TransactionDatabaseDetailBase
    {
        readonly INCommandDetail cmdDtl;
        readonly string sn;
        public Dictionary<int, long> ReadIndex = new();
        public FingerprintDatabaseDetail(INCommandDetail cmdDtl, string sn)
        {
            this.cmdDtl = cmdDtl;
            this.sn = sn;
        }
        /// <summary>
        /// 读取数据库详情
        /// </summary>
        /// <returns></returns>
        private async Task<TransactionDatabaseDetail> GetDatabaseDetail()
        {
            var cmd = new DoNetDrive.Protocol.Fingerprint.Transaction.ReadTransactionDatabaseDetail(cmdDtl);
            await CommandAllocator.Allocator.AddCommandAsync(cmd).ConfigureAwait(false);
            var result = (DoNetDrive.Protocol.Fingerprint.Transaction.ReadTransactionDatabaseDetail_Result)cmd.getResult();
            return result.DatabaseDetail;
        }
        /// <summary>
        /// 赋值人脸记录对象
        /// </summary>
        /// <param name="type"></param>
        /// <param name="transactionList"></param>
        /// <param name="item"></param>
        private void SetFaceTransaction(int type, Dictionary<int, CardRecord> transactionList, AbstractTransaction item)
        {
            FaceTransaction record;
            if (!transactionList.ContainsKey(item.SerialNumber))
            {
                if (type == 4)//体温记录没有匹配到认证记录
                    return;
                transactionList.Add(item.SerialNumber, new FaceTransaction());
            }
            record = GetFaceTransaction(transactionList, item);
            if (type != 4)
            {
                SetRecordMsg(item, record);
            }
            if (type == 1)//认证记录
            {
                SetCardTransaction(item, record);
            }
            else if (type == 4)//体温记录
            {
                if (record.UserCode != 0)
                    SetBodyTemperature(item, record);
            }
        }

        /// <summary>
        /// 记录对象转换
        /// </summary>
        /// <param name="transactionDic"></param>
        /// <returns></returns>
        private static List<CardRecord> ConvertToCardRecord(Dictionary<int, Dictionary<int, CardRecord>> transactionDic)
        {
            var result = new List<CardRecord>();
            foreach (var item in transactionDic)
            {
                result.AddRange(item.Value.Values.ToList());
            }
            return result;
        }

        /// <summary>
        /// 赋值记录消息
        /// </summary>
        /// <param name="item"></param>
        /// <param name="record"></param>
        private static void SetRecordMsg(AbstractTransaction item, FaceTransaction record)
        {
            record.RecordDate = item.TransactionDate.ToDateTimeStr();
            record.RecordMsg = MessageType.TransactionCodeNameList[item.TransactionType][item.TransactionCode];
        }

        /// <summary>
        /// 获取人脸记录
        /// </summary>
        /// <param name="transactionList"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        private FaceTransaction GetFaceTransaction(Dictionary<int, CardRecord> transactionList, AbstractTransaction item)
        {
            FaceTransaction record = (FaceTransaction)transactionList[item.SerialNumber];
            record.SN = sn;
            record.RecordCode = item.TransactionCode;
            record.RecordNumber = item.SerialNumber;
            if (record.RecordType == 0)
                record.RecordType = item.TransactionType;
            return record;
        }

        /// <summary>
        /// 赋值体温记录
        /// </summary>
        /// <param name="item"></param>
        /// <param name="record"></param>
        private static void SetBodyTemperature(AbstractTransaction item, FaceTransaction record)
        {
            var cardtrn = (DoNetDrive.Protocol.Fingerprint.Data.Transaction.BodyTemperatureTransaction)item;
            record.BodyTemperature = (double)cardtrn.BodyTemperature / 10;
        }

        /// <summary>
        /// 赋值记录内容
        /// </summary>
        /// <param name="item"></param>
        /// <param name="record"></param>
        private static void SetCardTransaction(AbstractTransaction item, FaceTransaction record)
        {
            var cardtrn = (DoNetDrive.Protocol.Fingerprint.Data.Transaction.CardTransaction)item;
            record.Accesstype = cardtrn.Accesstype;
            record.Photo = cardtrn.Photo;
            record.UserCode = cardtrn.UserCode;
        }

        /// <summary>
        /// 更新断点
        /// </summary>
        /// <returns></returns>
        public async Task SetReadIndex()
        {
            foreach (var item in ReadIndex)
            {
                if (item.Value <= 0)
                {
                    continue;
                }
                var type = item.Key + 1;
                var par = new DoNetDrive.Protocol.Fingerprint.Transaction.WriteTransactionDatabaseReadIndex_Parameter((DoNetDrive.Protocol.Fingerprint.Transaction.e_TransactionDatabaseType)type, (int)item.Value);
                var cmd = new DoNetDrive.Protocol.Fingerprint.Transaction.WriteTransactionDatabaseReadIndex(cmdDtl, par);
                await CommandAllocator.Allocator.AddCommandAsync(cmd);
            }
            ReadIndex.Clear();
        }


        /// <summary>
        /// 读取人脸指纹机记录
        /// </summary>
        /// <param name="cmdDtl"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private async Task<ReadTransactionDatabaseByIndex_Result> ReadFaceTransactionDataBase(int type, long readIndex)
        {
            try
            {
                var parameter = new DoNetDrive.Protocol.Fingerprint.Transaction.ReadTransactionDatabaseByIndex_Parameter(type, (int)readIndex + 1, 20);
                var cmd = new DoNetDrive.Protocol.Fingerprint.Transaction.ReadTransactionDatabaseByIndex(cmdDtl, parameter);
                await CommandAllocator.Allocator.AddCommandAsync(cmd);
                var result = (DoNetDrive.Protocol.Door.Door8800.Transaction.ReadTransactionDatabaseByIndex_Result)cmd.getResult();
                return result;
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// 读取记录
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<CardRecord>> ReadRecord()
        {
            var databaseDetail = await GetDatabaseDetail();
            var transactionDic = new Dictionary<int, Dictionary<int, CardRecord>>();
            for (int i = 0; i < 4; i++)
            {
                var transactionDetail = databaseDetail.ListTransaction[i];
                int type = i + 1;
                if (transactionDetail.WriteIndex - transactionDetail.ReadIndex <= 0 || !MyRegistry.Options.CheckFingerprint(type))//判断是否有新记录，大于0表示存在新记录
                {
                    continue;
                }
                if (type != 4)
                    transactionDic.Add(i, new Dictionary<int, CardRecord>());
                var database = await ReadFaceTransactionDataBase(type, transactionDetail.ReadIndex) ?? throw new Exception("Read Rcord Error");//根据索引号读取记录
                var index = type == 4 ? 0 : i;
                var transactionList = transactionDic.ContainsKey(index) ? transactionDic[index] : new Dictionary<int, CardRecord>();//将读取到的记录存储到字典中
                foreach (var item in database.TransactionList)
                {
                    SetFaceTransaction(type, transactionList, item);
                }
                ReadIndex.Add(i, database.TransactionList.Count + transactionDetail.ReadIndex);
            }
            return ConvertToCardRecord(transactionDic);
        }
    }
}
