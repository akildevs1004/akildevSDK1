﻿using DoNetDrive.Common.Extensions;
using DoNetDrive.Core;
using DoNetDrive.Core.Command;
using DoNetDrive.Core.Connector.TCPClient;
using FCardProtocolAPI.Command;
using FCardProtocolAPI.Command.Allocator;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;

namespace FCardProtocolAPI.Controllers
{
    //  [Route("api/[controller]")]
    [ApiController]
    public class CommandController : BaseController
    {
        public CommandController(IServiceProvider provider, Command.IDoor8900HCommand door8900H, Command.IFingerprintCommand fingerprint) : base(provider, door8900H, fingerprint)
        {

        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <returns></returns>
        [HttpPost("{SN}/{Command}")]
        public async Task<Command.IFcardCommandResult> Command()
        {

            try
            {
                if (!Request.RouteValues.ContainsKey("SN"))
                {
                    return GetCommandResultInstance("缺少设备SN", FCardProtocolAPI.Command.CommandStatus.ParameterError);
                }
                if (!Request.RouteValues.ContainsKey("Command"))
                {
                    return GetCommandResultInstance("缺少请求命令", FCardProtocolAPI.Command.CommandStatus.ParameterError);
                }
                var sn = Request.RouteValues["SN"]?.ToString();

                if (!CommandAllocator.TypeFunc.CheckDeviceSn(sn))
                {
                    return GetCommandResultInstance("没有对应的命令", FCardProtocolAPI.Command.CommandStatus.CommandError);
                }
                var command = Request.RouteValues["Command"]?.ToString();
                var body = ReadBody();
                var commandDetail = CommandDetailFunc.GetCommandDetail(sn);
                if (commandDetail == null)
                {
                    return GetCommandResultInstance("设备未连接到服务器或者未注册", FCardProtocolAPI.Command.CommandStatus.CommandError);
                }
                var parameter = _Provider.GetService(typeof(Command.IFcardCommandParameter)) as Command.IFcardCommandParameter;
                parameter.Command = command;
                parameter.Sn = sn;
                parameter.Data = body;
                parameter.CommandDetail = commandDetail;
                parameter.Allocator = CommandAllocator.Allocator;
                Command.IFcardCommand iCommand;
                if (CommandAllocator.TypeFunc.IsDoor8900H(sn))
                {
                    iCommand = _Door8900H;
                }
                else
                {
                    iCommand = _FingerprintCommand;
                }
                return await ExecutiveCommand(iCommand, parameter);
            }
            catch (Exception ex)
            {
                if (ex.Message.Equals("CommandStatus_Timeout"))
                {
                    return GetCommandResultInstance("Timeout", FCardProtocolAPI.Command.CommandStatus.CommonTimeout);
                }
                else
                {
                    return GetCommandResultInstance("Command Error：" + ex.Message, FCardProtocolAPI.Command.CommandStatus.CommandError);
                }
            }
        }
        /// <summary>
        /// websocket 连接
        /// </summary>
        /// <returns></returns>
        [HttpGet("/WebSocket")]
        public async Task WebSocket()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                //获取websocket对象
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var key = HttpContext.Connection.RemoteIpAddress + ":" + HttpContext.Connection.RemotePort;
                CommandAllocator.SocketFunc.WebSockets.TryAdd(key, webSocket);
                Console.WriteLine("websocket 客户端连接：" + key);
                while (true)
                {
                    try
                    {
                        byte[] recvBuffer = new byte[1024];
                        var recvAs = new ArraySegment<byte>(recvBuffer);
                        await webSocket.ReceiveAsync(recvAs, CancellationToken.None);
                    }
                    catch
                    {
                        break;
                    }
                }
                CommandAllocator.SocketFunc.WebSockets.TryRemove(key, out _);
                Console.WriteLine("websocket 客户端连接关闭：" + key);
            }
            else
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }
        /// <summary>
        /// 注册设备
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        [HttpPost("Register")]
        public Command.IFcardCommandResult Register([FromBody] Command.FcardCommandParameter parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter.Sn))
            {
                return GetCommandResultInstance("设备Sn不能为空", FCardProtocolAPI.Command.CommandStatus.ParameterError);
            }
            if (string.IsNullOrWhiteSpace(parameter.Ip))
            {
                return GetCommandResultInstance("设备Ip不能为空", FCardProtocolAPI.Command.CommandStatus.ParameterError);
            }

            var paht = "Devices.json";
            var jsonStr = System.IO.File.ReadAllText(paht);
            var deviceinfos = JsonConvert.DeserializeObject<FileDevicesInfo>(jsonStr);
            if (parameter.Port == null || parameter.Port == 0)
            {

                if (CommandAllocator.TypeFunc.IsDoor8900H(parameter.Sn))
                    parameter.Port = CommandAllocator.Options.TCPPort;
                else
                    parameter.Port = CommandAllocator.Options.UDPPort;
            }
            var deviceinfo = new DevicesInfo
            {
                SN = parameter.Sn,
                IP = parameter.Ip,
                Port = (int)parameter.Port,
            };
            //判断之前是否已经注册，已经注册就将内容进行替换
            if (CommandAllocator.DevicesInfos.ContainsKey(parameter.Sn))
            {
                CommandAllocator.DevicesInfos[parameter.Sn] = deviceinfo;
                deviceinfos.DevicesInfos.RemoveAll((a) => a.SN.Equals(parameter.Sn));
            }
            else
            {
                CommandAllocator.DevicesInfos.TryAdd(parameter.Sn, deviceinfo);
            }
            if (deviceinfos.DevicesInfos == null)
                deviceinfos.DevicesInfos = new List<DevicesInfo>();
            deviceinfos.DevicesInfos.Add(deviceinfo);
            string json = JsonConvert.SerializeObject(deviceinfos);
            System.IO.File.WriteAllText(paht, json);
            Console.WriteLine("注册设备：" + json);
            return GetCommandResultInstance("设备注册成功", FCardProtocolAPI.Command.CommandStatus.Succeed);
        }
        /// <summary>
        /// 获取设备列表
        /// </summary>
        /// <returns></returns>
        [HttpPost("GetDevices")]
        public Command.IFcardCommandResult GetDevices()
        {
            var list = new List<DevicesInfo>();
            foreach (var item in CommandAllocator.ClientList)
            {
                if ((DateTime.Now - item.Value.KeepAliveTime).TotalMinutes < 1.5)
                {
                    var tcp = (TCPClientDetail)item.Value.ConnectorDetail;
                    list.Add(new DevicesInfo
                    {
                        IP = tcp.Addr,
                        IsClient = true,
                        KeepAliveTime = item.Value.KeepAliveTime.ToDateTimeStr(),
                        Port = tcp.Port,
                        SN = item.Key
                    });
                }
            }
            ////获取tcp方式连接的设备
            //list.AddRange(CommandAllocator.TCPServerClientList.Select(a => new DevicesInfo { SN = a.Key, IP = a.Value.Remote.Addr, Port = a.Value.Remote.Port }));
            ////获取UPD方式连接的设备
            //list.AddRange(CommandAllocator.UDPServerClientList.Select(a => new DevicesInfo { SN = a.Key, IP = a.Value.Addr, Port = a.Value.Port }));
            //获取本地局域网注册的设备
            list.AddRange(CommandAllocator.DevicesInfos.Values);
            return GetCommandResultInstance("查询成功", FCardProtocolAPI.Command.CommandStatus.Succeed, list);
        }
        /// <summary>
        /// 检查设备是否在线
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [HttpPost("CheckDeviceHealth/{sn}")]
        public IFcardCommandResult CheckDeviceHealth(string sn)
        {
            var commandDetail = CommandDetailFunc.GetCommandDetail(sn);
            var result = commandDetail == null ? FCardProtocolAPI.Command.CommandStatus.ConnectionError : FCardProtocolAPI.Command.CommandStatus.Succeed;
            return GetCommandResultInstance(result.ToString(), result);
        }

        [HttpPost("Person/AddRange")]
        public IFcardCommandResult PersonAddRange([FromBody] PersonAddRangeInfo info)
        {
            try
            {
                if (info == null)
                {
                    return GetCommandResultInstance("Parameter Error", FCardProtocolAPI.Command.CommandStatus.ParameterError);
                }
                if (info.SNList == null || !info.SNList.Any())
                {
                    return GetCommandResultInstance("snList Parameter Error", FCardProtocolAPI.Command.CommandStatus.ParameterError);
                }

                if (info.PersonList == null || !info.PersonList.Any())
                {
                    return GetCommandResultInstance("PersonList Parameter Error", FCardProtocolAPI.Command.CommandStatus.ParameterError);
                }
                var result = new List<PersonAddRangeErrorInfo>();
                List<IFcardCommandParameter> list = new List<IFcardCommandParameter>();
                foreach (var sn in info.SNList)
                {
                    var commandDetail = CommandDetailFunc.GetCommandDetail(sn);
                    if (commandDetail == null)
                    {
                        result.Add(new PersonAddRangeErrorInfo
                        {
                            SN = sn,
                            Message = "The device was not found",
                            State = false
                        });
                    }
                    else
                    {
                        var parameter = new FcardCommandParameter();
                        parameter.Sn = sn;
                        parameter.CommandDetail = commandDetail;
                        list.Add(parameter);
                    }
                }
                if (list.Any())
                {
                    var resultList = _FingerprintCommand.AddPersonPlus(CommandAllocator.Allocator, list, info.PersonList);
                    result.AddRange(resultList.ToArray());
                }
                return GetCommandResultInstance("", FCardProtocolAPI.Command.CommandStatus.Succeed, result);
            }
            catch (Exception ex)
            {
                LogHelper.Error("批量添加人员异常：" + JsonConvert.SerializeObject(info), ex);
                return GetCommandResultInstance(ex.Message, FCardProtocolAPI.Command.CommandStatus.SystemError);

            }
        }

    }
}
