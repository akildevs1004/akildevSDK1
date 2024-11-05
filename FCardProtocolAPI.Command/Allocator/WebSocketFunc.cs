using DoNetDrive.Common;
using DoNetDrive.Common.Extensions;
using DoNetDrive.Core.Connector;
using DoNetDrive.Protocol.Door8800;
using FCardProtocolAPI.Command.Jobs;
using FCardProtocolAPI.Command.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Allocator
{
    public class WebSocketFunc
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        /// <summary>
        /// 已连接的websocket列表
        /// </summary>
        public ConcurrentDictionary<string, WebSocket> WebSockets = new ConcurrentDictionary<string, WebSocket>();
        /// <summary>
        /// 已连接的数量
        /// </summary>
        /// <returns></returns>
        public int ConnetionCount { get { return WebSockets.Count; } }


        /// <summary>
        /// 发送webSocket消息
        /// </summary>
        /// <param name="result"></param>
        public async Task SendWebSocketMessage(IFcardCommandResult result)
        {
            var message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result, Settings));
            var buf = new ArraySegment<byte>(message);
            foreach (var item in WebSockets)
            {
                await item.Value.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async void KeepAliveTransaction(Door8800Transaction transaction)
        {
            if (WebSockets.Count <= 0) return;
            var record = new KeepAliveTransaction
            {
                SN = transaction.SN,
                KeepAliveTime = transaction.EventData.TransactionDate
            };
            await SendWebSocketMessage(GetResult(CommandStatus.Succeed, TransactionType.KeepAliveTransaction, record));
        }

        public async void FaceSystemTransaction(Door8800Transaction transaction)
        {
            if (WebSockets.Count <= 0) return;
            if (CommandAllocator.TypeFunc.IsDoor8900H(transaction.SN))
            {
                return;
            }

            var t = (DoNetDrive.Protocol.Fingerprint.Data.Transaction.SystemTransaction)transaction.EventData;
            var record = new CardRecord()
            {
                RecordType = 3,
                Door = t.Door,
                RecordCode = t.TransactionCode,
                RecordDate = t.TransactionDate.ToDateTimeStr(),
                SN = transaction.SN
            };
            await SendWebSocketMessage(GetResult(CommandStatus.Succeed, TransactionType.SystemTransaction, record));
        }

        public static IFcardCommandResult GetResult(Command.CommandStatus status, TransactionType type, object data = null)
        {
            var result = new FcardCommandResult();
            result.Message = "Record Message";
            result.Status = status;
            result.Data = data;
            result.TransactionType = type;
            return result;
        }
    }
}
