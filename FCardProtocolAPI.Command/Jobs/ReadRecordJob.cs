using DoNetDrive.Common.Extensions;
using DoNetDrive.Core.Command;
using DoNetDrive.Core.Connector;
using DoNetDrive.Protocol.Door.Door8800.Data;
using DoNetDrive.Protocol.Door.Door8800.Transaction;
using DoNetDrive.Protocol.Fingerprint.AdditionalData;
using DoNetDrive.Protocol.Transaction;
using FCardProtocolAPI.Command.Allocator;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using FluentScheduler;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace FCardProtocolAPI.Command.Jobs
{
    public class ReadRecordJob : IJob
    {

        public void Execute()
        {
            if (CommandAllocator.SocketFunc.ConnetionCount <= 0 || CommandAllocator.ClientList.IsEmpty)
            {
                return;
            }
            var keys = CommandAllocator.ClientList.Keys;
            var tasks = new List<Task>();
            foreach (var key in keys)
            {
                if (CommandAllocator.ClientList.TryGetValue(key, out var value))
                {
                    if (string.IsNullOrWhiteSpace(value.SN))
                        continue;
                    tasks.Add(ReadRecord(detail: value));
                }
            }
            Task.WaitAll(tasks.ToArray(), keys.Count * 60000);
        }
        /// <summary>
        /// 读取记录
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static async Task ReadRecord(MyConnectorDetail detail)
        {
            try
            {
                if (!CommandAllocator.TypeFunc.CheckDeviceSn(detail.SN))
                {
                    return;
                }
                var cmdDtl = CommandDetailFunc.GetCommandDetail(detail.SN, detail.ConnectorDetail);
                var transactionList = new List<CardRecord>();
                TransactionDatabaseDetailBase readTransaction;
                if (CommandAllocator.TypeFunc.IsDoor8900H(detail.SN))
                {
                    readTransaction = new DoorDatabaseDetail(cmdDtl, detail.SN);
                }
                else
                {
                    readTransaction = new FingerprintDatabaseDetail(cmdDtl, detail.SN);
                }

                var records = await readTransaction.ReadRecord();//读取记录
                transactionList.AddRange(records);
                await Send(cmdDtl, transactionList);//发送记录
                await readTransaction.SetReadIndex();//更新记录断点
            }
            catch (Exception ex)
            {
                LogHelper.Error("Read Record Job", ex);
            }
        }
        /// <summary>
        /// 发送记录
        /// </summary>
        /// <param name="transactionList"></param>
        /// <returns></returns>
        private static async Task Send(INCommandDetail cmdDtl, List<CardRecord> transactionList)
        {
            if (!transactionList.Any())
            {
                return;
            }
            foreach (var item in transactionList)
            {
                TransactionType type = TransactionType.CardTransaction;
                if (item is FaceTransaction faceTransaction)
                {
                    type = TransactionType.FaceTransaction;
                    if (faceTransaction.Photo == 1 && MyRegistry.Options.RecordImage)
                    {
                        faceTransaction.RecordImage = await ReadRecordImage(cmdDtl, (uint)faceTransaction.RecordNumber);
                    }
                }
                var result = WebSocketFunc.GetResult(CommandStatus.Succeed, type, item);
                await CommandAllocator.SocketFunc.SendWebSocketMessage(result);
                LogHelper.Info("push record" + (JsonConvert.SerializeObject(item)));
                item.Release();
            }
        }


        /// <summary>
        /// 读取图片
        /// </summary>
        /// <param name="cmdDtl"></param>
        /// <param name="recordNumber"></param>
        /// <returns></returns>
        private async static Task<byte[]> ReadRecordImage(INCommandDetail cmdDtl, uint recordNumber)
        {
            var cmd = new ReadFile(cmdDtl, new ReadFile_Parameter(recordNumber, 3, 1));
            await CommandAllocator.Allocator.AddCommandAsync(cmd);
            if (cmd.getResult() is ReadFile_Result result && result.FileHandle != 0 && result.Result)
            {
                return result.FileDatas;
            }
            return null;
        }
    }
}
