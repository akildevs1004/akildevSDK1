using DoNetDrive.Common;
using DoNetDrive.Common.Extensions;
using DoNetDrive.Core.Command;
using DoNetDrive.Protocol.Door.Door8800.Data;
using DoNetDrive.Protocol.Door.Door8800.Transaction;
using DoNetDrive.Protocol.Transaction;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace FCardProtocolAPI.Command.Jobs
{
    public class DoorDatabaseDetail : TransactionDatabaseDetailBase
    {
        readonly INCommandDetail cmdDtl;
        readonly string sn;
        public Dictionary<int, long> ReadIndex = new Dictionary<int, long>();
        public DoorDatabaseDetail(INCommandDetail cmdDtl, string sn)
        {
            this.cmdDtl = cmdDtl;
            this.sn = sn;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<TransactionDatabaseDetail> GetDatabaseDetail()
        {
            try
            {
                var cmd = new ReadTransactionDatabaseDetail(cmdDtl);
                await CommandAllocator.Allocator.AddCommandAsync(cmd).ConfigureAwait(false);
                var result = (ReadTransactionDatabaseDetail_Result)cmd.getResult();
                return result.DatabaseDetail;
            }
            catch
            {

                return null;
            }
        }



        private static List<CardRecord> CardRecordToList(Dictionary<int, Dictionary<int, CardRecord>> transactionDic)
        {
            var result = new List<CardRecord>();
            foreach (var item in transactionDic)
            {
                result.AddRange(item.Value.Values.ToList());
            }
            return result;
        }

        private async Task<ReadTransactionDatabaseByIndex_Result> ReadTransactionDataBase(long readIndex, int type)
        {
            var parameter = new ReadTransactionDatabaseByIndex_Parameter(type, (int)readIndex + 1, 60);
            var cmd = new DoNetDrive.Protocol.Door.Door89H.Transaction.ReadTransactionDatabaseByIndex(cmdDtl, parameter);
            await CommandAllocator.Allocator.AddCommandAsync(cmd);
            var result = (ReadTransactionDatabaseByIndex_Result)cmd.getResult();
            return result;
        }


        public async Task SetReadIndex()
        {
            foreach (var item in ReadIndex)
            {
                if (item.Value <= 0)
                {
                    continue;
                }
                var type = item.Key + 1;
                var par = new WriteTransactionDatabaseReadIndex_Parameter((e_TransactionDatabaseType)type, (int)item.Value, true);
                var cmd = new WriteTransactionDatabaseReadIndex(cmdDtl, par);
                await CommandAllocator.Allocator.AddCommandAsync(cmd);
            }

        }

        public async Task<List<CardRecord>> ReadRecord()
        {
            var databaseDetail = await GetDatabaseDetail();
            var transactionDic = new Dictionary<int, Dictionary<int, CardRecord>>();
            for (int i = 0; i < 6; i++)
            {
                int type = i + 1;
                var transactionDetail = databaseDetail.ListTransaction[i];
                if (transactionDetail.WriteIndex - transactionDetail.ReadIndex <= 0 && MyRegistry.Options.CheckDoor(type))
                {
                    continue;
                }
                var database = await ReadTransactionDataBase(transactionDetail.ReadIndex, type);
                transactionDic.Add(i, new Dictionary<int, CardRecord>());
                var transactionList = transactionDic[i];
                foreach (var item in database.TransactionList)
                {
                    SetCardRecord(type, transactionList, item);
                }
                ReadIndex.Add(i, transactionList.Count + transactionDetail.ReadIndex);
            }
            return CardRecordToList(transactionDic);

        }

        private void SetCardRecord(int type, Dictionary<int, CardRecord> transactionList, AbstractTransaction item)
        {
            CardRecord record;
            if (!transactionList.ContainsKey(item.SerialNumber))
            {
                transactionList.Add(item.SerialNumber, new CardRecord());
            }
            record = transactionList[item.SerialNumber];
            record.RecordNumber = item.SerialNumber;
            record.RecordDate = item.TransactionDate.ToDateTimeStr();
            record.RecordType = item.TransactionType;
            record.RecordCode = item.TransactionCode;
            record.RecordMsg = MessageType.CardTransactionCodeList[item.TransactionType][item.TransactionCode];
            record.SN = sn;
            if (type == 1)
            {
                var transaction = item as DoNetDrive.Protocol.Door.Door89H.Data.CardTransaction;
                record.Accesstype = (byte)(transaction.IsEnter() ? 1 : 2);
                if (transaction.IsPasswordCode)
                {
                    record.CardData = transaction.Password;
                }
                else
                {
                    record.CardData = transaction.BigCard.UInt64Value.ToString();
                }
                record.Door = transaction.DoorNum();
            }
        }
    }
}
