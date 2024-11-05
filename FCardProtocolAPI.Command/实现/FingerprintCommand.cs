﻿using DoNetDrive.Common.Extensions;
using DoNetDrive.Core;
using DoNetDrive.Core.Command;
using DoNetDrive.Protocol.Door.Door8800.Data.TimeGroup;
using DoNetDrive.Protocol.Door.Door8800.SystemParameter.KeepAliveInterval;
using DoNetDrive.Protocol.Door.Door8800.SystemParameter.Watch;
using DoNetDrive.Protocol.Door.Door8800.Time;
using DoNetDrive.Protocol.Door.Door8800.TimeGroup;
using DoNetDrive.Protocol.Fingerprint.AdditionalData;
using DoNetDrive.Protocol.Fingerprint.Alarm;
using DoNetDrive.Protocol.Fingerprint.Data;
using DoNetDrive.Protocol.Fingerprint.Data.Transaction;
using DoNetDrive.Protocol.Fingerprint.Door.Remote;
using DoNetDrive.Protocol.Fingerprint.Person;
using DoNetDrive.Protocol.Fingerprint.SystemParameter;
using DoNetDrive.Protocol.Fingerprint.SystemParameter.ManageMenuPassword;
using DoNetDrive.Protocol.Fingerprint.SystemParameter.OEM;
using DoNetDrive.Protocol.Fingerprint.Transaction;
using DoNetDrive.Protocol.OnlineAccess;
using FCardProtocolAPI.Command.Jobs;
using FCardProtocolAPI.Command.Models;
using FCardProtocolAPI.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command
{
    public class FingerprintCommand : IFingerprintCommand
    {

        private static string[] PersonUploadStatus = new string[] {
            "",
        "上传完毕",
        "特征码无法识别",
        "人员照片不可识别",
        "人员照片或特征码重复"
        };


        /// <summary>
        /// 添加人员
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> AddPerson(IFcardCommandParameter parameter)
        {
            try
            {
                var check = CheckPersonnelParameter(parameter, out var commandParameter);

                if (!check.Item1)
                {
                    return new FcardCommandResult
                    {
                        Message = check.Item2,
                        Status = CommandStatus.ParameterError
                    };
                }
                INCommand cmd;
                if (commandParameter is AddPerson_Parameter)
                {
                    cmd = new AddPerson(parameter.CommandDetail, (AddPerson_Parameter)commandParameter);
                    if (check.Item3.IsDeleteFace == true)
                    {
                        byte[] fingerprintList = new byte[10];
                        byte[] palmList = new byte[2];//掌静脉特征码
                        byte[] photoList = new byte[5];
                        Array.Fill(photoList, (byte)1);
                        var isOk = await DeleteFeatureCode(parameter, new DeleteFeatureCode_Parameter((uint)check.Item3.userCode, photoList, fingerprintList, true, palmList));

                        return new FcardCommandResult
                        {
                            Message = isOk ? "删除人脸成功" : "删除失败",
                            Status = isOk ? CommandStatus.Succeed : CommandStatus.SystemError
                        };
                    }
                }
                else
                {
                    cmd = new AddPeosonAndImage(parameter.CommandDetail, (AddPersonAndImage_Parameter)commandParameter);
                }
                await parameter.Allocator.AddCommandAsync(cmd);
                var result = cmd.getResult();
                string message;
                if (result is AddPersonAndImage_Result)
                {
                    var r1 = result as AddPersonAndImage_Result;
                    message = $"人员上传{(r1.UserUploadStatus ? "成功" : "失败")}";
                    if (r1.IdDataRepeatUser[0] > 0)
                    {
                        message += "，用户号重复";
                    }
                    if (r1.IdDataUploadStatus[0] > 0)
                    {
                        message += "，识别信息上传状态:" + PersonUploadStatus[r1.IdDataUploadStatus[0]];
                    }
                    return new FcardCommandResult
                    {
                        Status = CommandStatus.Succeed,
                        Data = new
                        {
                            r1.UserUploadStatus,
                            IdDataRepeatUser = r1.IdDataRepeatUser[0],
                            IdDataUploadStatus = r1.IdDataUploadStatus[0],
                        },
                        Message = message
                    };
                }
                else
                {
                    var r2 = result as WritePerson_Result;
                    var UserUploadStatus = r2.FailTotal == 0;
                    message = $"人员上传{(UserUploadStatus ? "成功" : "失败")}";
                    return new FcardCommandResult
                    {
                        Data = new
                        {
                            UserUploadStatus,
                            IdDataRepeatUser = 0,
                            IdDataUploadStatus = 0
                        },
                        Message = message,
                        Status = CommandStatus.Succeed
                    };

                }
            }
            catch (Exception ex)
            {
                return new FcardCommandResult
                {
                    Message = "参数错误，请检查参数",
                    Status = CommandStatus.ParameterError
                };
            }
        }


        /// <summary>
        /// 检查人员参数
        /// </summary>
        /// <param name="iParameter"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private (bool, string, PersonModel) CheckPersonnelParameter(IFcardCommandParameter iParameter, out DoNetDrive.Protocol.Door.Door8800.AbstractParameter parameter)
        {
            (bool, string, PersonModel) result = (false, null, null);
            parameter = null;
            try
            {

                var p = JsonConvert.DeserializeObject<Models.PersonModel>(iParameter.Data);
                if (p == null)
                {
                    result.Item2 = "人员信息有误请检查";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(p.name))
                {
                    result.Item2 = "缺少人员名称";
                    return result;
                }
                if (!uint.TryParse(p.userCode?.ToString(), out var userCode))
                {
                    result.Item2 = "缺少用户编号/用户编号有误";
                    return result;
                }
                var person = new Person();
                if (!string.IsNullOrWhiteSpace(p.code))
                {
                    if (!int.TryParse(p.code, out _))
                    {
                        result.Item2 = "人员编号必须是数字";
                        return result;
                    }
                    person.PCode = p.code;
                }

                person.PName = p.name;
                person.UserCode = userCode;
                if (!string.IsNullOrWhiteSpace(p.cardData))
                {
                    if (ulong.TryParse(p.cardData, out var cardData))
                    {
                        if (cardData < 0x1 || cardData >= 0xFFFFFFFF)
                        {
                            result.Item2 = "卡号超出取值范围：0x1-0xFFFFFFFF";
                            return result;
                        }
                        person.CardData = cardData;
                    }
                    else
                    {
                        result.Item2 = "卡号必须是十进制数字";
                        return result;
                    }
                }
                if (!string.IsNullOrWhiteSpace(p.password))
                {
                    if (p.password.Length >= 4 && p.password.Length <= 8)
                    {
                        if (!int.TryParse(p.password, out var password))
                        {
                            result.Item2 = "密码是4-8位的数字";
                            return result;
                        }
                        person.Password = p.password;
                    }
                    else
                    {
                        result.Item2 = "密码长度不够：密码是4-8位的数字";
                        return result;
                    }
                }
                if (p.identity != null)
                {
                    if (p.identity > 1 || p.identity < 0)
                    {
                        result.Item2 = "用户身份有误";
                        return result;
                    }
                    person.Identity = (int)p.identity;
                }
                if (p.cardStatus != null)
                {
                    if (p.cardStatus > 3 || p.cardStatus < 0)
                    {
                        result.Item2 = "卡片状态有误";
                        return result;
                    }
                    person.CardStatus = (int)p.cardStatus;
                }
                if (p.cardType != null)
                {
                    if (p.cardType > 1 || p.cardType < 0)
                    {
                        result.Item2 = "卡片状态有误";
                        return result;
                    }
                    person.CardType = (int)p.cardType;
                }
                if (p.enterStatus != null)
                {
                    if (p.enterStatus > 2 || p.enterStatus < 0)
                    {
                        result.Item2 = "出入标记有误";
                        return result;
                    }
                    person.EnterStatus = (int)p.enterStatus;
                }
                if (!string.IsNullOrWhiteSpace(p.expiry))
                {
                    if (DateTime.TryParse(p.expiry, out var expiry))
                    {
                        var time = DateTime.Parse("2089-12-31");
                        if (expiry > time)
                        {
                            expiry = time;
                        }
                        person.Expiry = expiry;
                    }
                    else
                    {
                        result.Item2 = "出入截止日期有误";
                        return result;
                    }
                }
                if (p.openTimes != null)
                {
                    if (p.openTimes > 65535 || p.openTimes < 0)
                    {
                        result.Item2 = "有效次数有误";
                        return result;
                    }
                    person.OpenTimes = (ushort)p.openTimes;
                }
                if (p.timeGroup != null)
                {
                    if (p.timeGroup < 0 || p.timeGroup > 64)
                    {
                        result.Item2 = "开门时段有误";
                        return result;
                    }
                    person.TimeGroup = (int)p.timeGroup;
                }
                for (int i = 0; i < 32; i++)
                {
                    person.SetHolidayValue(i + 1, false);
                }
                person.Job = p.job;
                person.Dept = p.dept;
                IdentificationData[] identifications = null;
                if (!p.IsDeleteFace && (!string.IsNullOrWhiteSpace(p.faceImage) || p.fp != null))
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(p.faceImage))
                        {
                            byte[] faceData;
                            if (!p.faceImage.IsBase64(out faceData))
                            {
                                faceData = HttpHelper.GetByteArray(p.faceImage);
                                if (faceData == null)
                                {
                                    result.Item2 = $"{p.faceImage} get image error";
                                    return result;
                                }
                            }
                            faceData = ImageTool.ConvertImage(faceData);
                            identifications = new IdentificationData[1];
                            identifications[0] = new IdentificationData(1, faceData);
                        }
                        else
                        {
                            if (p.fp.Length > 3 || p.fp.Length <= 0)
                            {
                                result.Item2 = "指纹特征码数量错误：上传特征码数量不能低于0或者超过3个";
                                return result;
                            }
                            identifications = new IdentificationData[p.fp.Length];
                            for (int i = 0; i < identifications.Length; i++)
                            {
                                var fpData = Convert.FromBase64String(p.fp[i]);
                                identifications[i] = new IdentificationData(2, i, fpData);
                            }
                        }
                    }
                    catch
                    {
                        result.Item2 = "人员图片有误";
                        return result;
                    }
                    parameter = new AddPersonAndImage_Parameter(person, identifications);
                }
                else
                {
                    parameter = new AddPerson_Parameter(new List<Person>() { person });
                }
                result.Item1 = true;
                result.Item3 = p;
            }
            catch (Exception ex)
            {
                result.Item2 = "参数异常，请检查参数";
            }

            return result;
        }



        /// <summary>
        /// 添加多设备多人员信息
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="commandParameters"></param>
        /// <param name="personModelList"></param>
        /// <returns></returns>
        public List<PersonAddRangeErrorInfo> AddPersonPlus(ConnectorAllocator allocator, List<IFcardCommandParameter> commandParameters, List<PersonModel> personModelList)
        {
            var personListInfo = GetPersonListInfo(personModelList);
            var personUploadResult = GetPersonUploadResult(personListInfo.ErrorList, commandParameters);
            if (personListInfo.PersonList.Any())
            {
                var taskList = new List<Task>();
                foreach (var cmdPar in commandParameters)
                {
                    taskList.Add(Task.Run(async () =>
                    {
                        var errorInfo = personUploadResult.FirstOrDefault(a => a.SN == cmdPar.Sn);
                        if (errorInfo != null)
                            await UploadPerson(allocator, cmdPar, errorInfo, personListInfo.PersonList);
                    }));
                }
                Task.WaitAll(taskList.ToArray());
                taskList.Clear();
                foreach (var person in personListInfo.PersonList)
                {
                    var p = personModelList.FirstOrDefault(a => a.userCode == person.UserCode);
                    if (p != null && !p.faceImage.IsNullOrEmpty())
                    {
                        byte[] image = GetImageBytes(p.faceImage);
                        foreach (var cmdPar in commandParameters)
                        {
                            var errorInfo = personUploadResult.FirstOrDefault(a => a.SN == cmdPar.Sn);
                            if (errorInfo != null)
                            {
                                var user = errorInfo.UserList.FirstOrDefault(a => a.UserCode == person.UserCode);
                                if (user == null) //判断是否已经在错误列表中，不在列表中则执行上传图片
                                {
                                    if (image == null) //找不到图片
                                    {
                                        errorInfo.UserList.Add(new PersonAddErrorInfo
                                        {
                                            UserCode = person.UserCode,
                                            Message = $"{p.faceImage} get image error"
                                        });
                                    }
                                    else
                                    {
                                        taskList.Add(UploadPersonImage(allocator, cmdPar, errorInfo, person.UserCode, image));
                                    }
                                }
                            }
                        }
                    }
                }
                if (taskList.Any())
                {
                    Task.WaitAll(taskList.ToArray());
                    taskList.Clear();
                }
            }
            foreach (var item in personUploadResult)
            {
                if (item.UserList == null || !item.UserList.Any())
                {
                    item.State = true;
                    item.Message = string.Empty;
                }
            }
            return personUploadResult;
        }



        /// <summary>
        /// 上传人员照片
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="commandParameter"></param>
        /// <param name="errorInfo"></param>
        /// <param name="userCode"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        private async Task UploadPersonImage(ConnectorAllocator allocator, IFcardCommandParameter commandParameter, PersonAddRangeErrorInfo errorInfo, uint userCode, byte[] image)
        {
            try
            {
                //  Debug.WriteLine($"sn={commandParameter.Sn},usercode={userCode}");
                var par = new WriteFeatureCode_Parameter(userCode, 1, 1, image);
                par.WaitRepeatMessage = true;
                var cmd = new WriteFeatureCode(commandParameter.CommandDetail, par);
                await allocator.AddCommandAsync(cmd);
                var result = (WriteFeatureCode_Result)cmd.getResult();
                if (result.Result != 1)
                {
                    var personAddErrorInfo = new PersonAddErrorInfo
                    {
                        UserCode = userCode,
                        Message = PersonUploadStatus[result.Result]
                    };
                    if (result.Result == 4)
                        personAddErrorInfo.Message += $"RepeatUser[{result.RepeatUser}]";
                    errorInfo.UserList.Add(personAddErrorInfo);
                    //  Debug.WriteLine(JsonConvert.SerializeObject(personAddErrorInfo));
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"批量添加人员上传照片异常：用户编号：{userCode},上传图片内容{image}", ex);
                var personAddErrorInfo = new PersonAddErrorInfo
                {
                    UserCode = userCode,
                    Message = "上传图片异常:" + ex.Message
                };
                errorInfo.UserList.Add(personAddErrorInfo);
            }

        }
        /// <summary>
        /// 获取照片信息
        /// </summary>
        /// <param name="faceImage"></param>
        /// <returns></returns>
        private byte[] GetImageBytes(string faceImage)
        {
            byte[] faceData;
            if (!faceImage.IsBase64(out faceData))
            {
                faceData = HttpHelper.GetByteArray(faceImage);
                if (faceData == null)
                {
                    return faceData;
                }
            }
            faceData = ImageTool.ConvertImage(faceData);
            return faceData;
        }
        /// <summary>
        /// 上传人员信息
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="commandParameter"></param>
        /// <param name="errorInfo"></param>
        /// <param name="personList"></param>
        /// <returns></returns>
        private async Task UploadPerson(ConnectorAllocator allocator, IFcardCommandParameter commandParameter, PersonAddRangeErrorInfo errorInfo, List<Person> personList)
        {
            try
            {
                var parameter = new AddPerson_Parameter(personList);
                var cmd = new AddPerson(commandParameter.CommandDetail, parameter);
                await allocator.AddCommandAsync(cmd);
                var result = (WritePerson_Result)cmd.getResult();
                if (result.PersonList.Any())
                {
                    SetErrorPerson(errorInfo, result.PersonList);
                }
            }
            catch (Exception ex)
            {
                errorInfo.State = false;
                errorInfo.Message = "person upload error:" + ex.Message;
                LogHelper.Error("上传人员信息异常:", ex);
            }
        }

        private static void SetErrorPerson(PersonAddRangeErrorInfo errorInfo, List<uint> users)
        {
            errorInfo.State = false;
            errorInfo.Message = "person upload error";
            foreach (var item in users)
            {
                errorInfo.UserList.Add(new PersonAddErrorInfo
                {
                    UserCode = item,
                    Message = "add error"
                });
            }
        }


        /// <summary>
        /// 获取上传人员返回结果对象列表
        /// </summary>
        /// <param name="errorInfos"></param>
        /// <param name="commandParameters"></param>
        /// <returns></returns>
        private List<PersonAddRangeErrorInfo> GetPersonUploadResult(List<PersonAddErrorInfo> errorInfos, List<IFcardCommandParameter> commandParameters)
        {
            var list = new List<PersonAddRangeErrorInfo>();
            foreach (var item in commandParameters)
            {
                list.Add(new PersonAddRangeErrorInfo
                {
                    State = false,
                    Message = "person info error",
                    SN = item.Sn,
                    UserList = new List<PersonAddErrorInfo>(errorInfos)
                });
            }
            return list;
        }
        /// <summary>
        /// 获取人员列表
        /// </summary>
        /// <param name="personModelList"></param>
        /// <returns></returns>
        private PersonListInfo GetPersonListInfo(List<PersonModel> personModelList)
        {
            var personList = new List<Person>();
            var errorList = new List<PersonAddErrorInfo>();
            foreach (var model in personModelList)
            {
                var result = CheckPersonnelParameter(model, out var person);
                if (result.Item1)
                {
                    personList.Add(person);
                }
                else
                {
                    errorList.Add(new PersonAddErrorInfo
                    {
                        Message = result.Item2,
                        UserCode = (uint)model.userCode
                    });
                }
            }
            return new PersonListInfo
            {
                ErrorList = errorList,
                PersonList = personList
            };
        }
        /// <summary>
        /// 检查人员参数
        /// </summary>
        /// <param name="p"></param>
        /// <param name="person"></param>
        /// <returns></returns>
        private (bool, string) CheckPersonnelParameter(PersonModel p, out Person person)
        {
            person = new Person();
            (bool, string) result = (false, null);
            if (string.IsNullOrWhiteSpace(p.name))
            {
                result.Item2 = "缺少人员名称";
                return result;
            }
            if (!uint.TryParse(p.userCode?.ToString(), out var userCode))
            {
                result.Item2 = "缺少用户编号/用户编号有误";
                return result;
            }
            if (!string.IsNullOrWhiteSpace(p.code))
            {
                if (!int.TryParse(p.code, out _))
                {
                    result.Item2 = "人员编号必须是数字";
                    return result;
                }
                person.PCode = p.code;
            }
            person.PName = p.name;
            person.UserCode = userCode;
            if (!string.IsNullOrWhiteSpace(p.cardData))
            {
                if (ulong.TryParse(p.cardData, out var cardData))
                {
                    if (cardData < 0x1 || cardData >= 0xFFFFFFFF)
                    {
                        result.Item2 = "卡号超出取值范围：0x1-0xFFFFFFFF";
                        return result;
                    }
                    person.CardData = cardData;
                }
                else
                {
                    result.Item2 = "卡号必须是十进制数字";
                    return result;
                }
            }
            if (!string.IsNullOrWhiteSpace(p.password))
            {
                if (p.password.Length >= 4 && p.password.Length <= 8)
                {
                    if (!int.TryParse(p.password, out var password))
                    {
                        result.Item2 = "密码是4-8位的数字";
                        return result;
                    }
                    person.Password = p.password;
                }
                else
                {
                    result.Item2 = "密码长度不够：密码是4-8位的数字";
                    return result;
                }
            }
            if (p.identity != null)
            {
                if (p.identity > 1 || p.identity < 0)
                {
                    result.Item2 = "用户身份有误";
                    return result;
                }
                person.Identity = (int)p.identity;
            }
            if (p.cardStatus != null)
            {
                if (p.cardStatus > 3 || p.cardStatus < 0)
                {
                    result.Item2 = "卡片状态有误";
                    return result;
                }
                person.CardStatus = (int)p.cardStatus;
            }
            if (p.cardType != null)
            {
                if (p.cardType > 1 || p.cardType < 0)
                {
                    result.Item2 = "卡片状态有误";
                    return result;
                }
                person.CardType = (int)p.cardType;
            }
            if (p.enterStatus != null)
            {
                if (p.enterStatus > 2 || p.enterStatus < 0)
                {
                    result.Item2 = "出入标记有误";
                    return result;
                }
                person.EnterStatus = (int)p.enterStatus;
            }
            if (!string.IsNullOrWhiteSpace(p.expiry))
            {
                if (DateTime.TryParse(p.expiry, out var expiry))
                {
                    var time = DateTime.Parse("2089-12-31");
                    if (expiry > time)
                    {
                        expiry = time;
                    }
                    person.Expiry = expiry;
                }
                else
                {
                    result.Item2 = "出入截止日期有误";
                    return result;
                }
            }
            if (p.openTimes != null)
            {
                if (p.openTimes > 65535 || p.openTimes < 0)
                {
                    result.Item2 = "有效次数有误";
                    return result;
                }
                person.OpenTimes = (ushort)p.openTimes;
            }
            if (p.timeGroup != null)
            {
                if (p.timeGroup < 0 || p.timeGroup > 64)
                {
                    result.Item2 = "开门时段有误";
                    return result;
                }
                person.TimeGroup = (int)p.timeGroup;
            }
            for (int i = 0; i < 32; i++)
            {
                person.SetHolidayValue(i + 1, false);
            }
            person.Job = p.job;
            person.Dept = p.dept;
            result.Item1 = true;
            return result;
        }


        /// <summary>
        /// 远程关门
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> CloseDoor(IFcardCommandParameter parameter)
        {
            CloseDoor cmd = new(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "关门成功"
            };
        }
        /// <summary>
        /// 删除人员
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> DeletePerson(IFcardCommandParameter parameter)
        {
            var data = JObject.Parse(parameter.Data.ToString());
            Dictionary<string, object> d = new Dictionary<string, object>(data.ToObject<IDictionary<string, object>>(), StringComparer.CurrentCultureIgnoreCase);
            if (!d.ContainsKey("userCodeArray"))
            {
                return new FcardCommandResult
                {
                    Status = CommandStatus.ParameterError,
                    Message = "参数格式错误"
                };
            }
            var array = JArray.Parse(d["userCodeArray"].ToString());
            List<Person> personList = new List<Person>();
            foreach (var code in array)
            {
                if (uint.TryParse(code.ToString(), out var uCode))
                    personList.Add(new Person { UserCode = uCode });
            }
            var cmd = new DeletePerson(parameter.CommandDetail, new DeletePerson_Parameter(personList));
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "删除成功"
            };
        }

        /// <summary>
        /// 删除所有人员
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> DeleteAllPerson(IFcardCommandParameter parameter)
        {
            var cmd = new ClearPersonDataBase(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "清空成功"
            };
        }

        /// <summary>
        /// 获取人员存储详情
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> GetDataBaseDetali(IFcardCommandParameter parameter)
        {
            var cmd = new ReadPersonDatabaseDetail(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = (ReadPersonDatabaseDetail_Result)cmd.getResult();
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "读取成功",
                Data = new
                {
                    result.SortDataBaseSize,
                    result.SortPersonSize,
                    result.SortFingerprintDataBaseSize,
                    result.SortFingerprintSize,
                    result.SortFaceDataBaseSize,
                    result.SortFaceSize,
                    result.PalmDataBaseSize,
                    result.PalmSize
                }
            };
        }
        /// <summary>
        /// 获取人员详情
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> GetPersonDetail(IFcardCommandParameter parameter)
        {
            var data = JObject.Parse(parameter.Data.ToString());
            Dictionary<string, string> d = new Dictionary<string, string>(data.ToObject<IDictionary<string, string>>(), StringComparer.CurrentCultureIgnoreCase);
            if (!d.ContainsKey("usercode") || !uint.TryParse(d["usercode"], out var iUserCode))
            {
                return new FcardCommandResult
                {
                    Status = CommandStatus.ParameterError,
                    Message = "参数格式错误"
                };
            }
            var par = new DoNetDrive.Protocol.Fingerprint.Person.ReadPersonDetail_Parameter(iUserCode);
            var cmd = new DoNetDrive.Protocol.Fingerprint.Person.ReadPersonDetail(parameter.CommandDetail, par);
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as DoNetDrive.Protocol.Fingerprint.Person.ReadPersonDetail_Result;
            if (!result.IsReady)
            {
                return new FcardCommandResult
                {
                    Message = $"没有找到编号为【{iUserCode}】的人员信息",
                    Status = CommandStatus.CommandError
                };
            }
            var p = result.Person;
            var person = new Models.PersonModel
            {
                name = p.PName,
                cardData = p.CardData.ToString(),
                cardStatus = p.CardStatus,
                cardType = p.CardType,
                code = p.PCode,
                dept = p.Dept,
                enterStatus = p.EnterStatus,
                expiry = p.Expiry.ToString("yyyy-MM-dd HH:mm:ss"),
                identity = p.Identity,
                job = p.Job,
                openTimes = p.OpenTimes,
                password = p.Password,
                timeGroup = p.TimeGroup,
                userCode = (uint)p.UserCode,
                face = p.IsFaceFeatureCode ? 1 : 0,
                fpCount = p.FingerprintFeatureCodeCout
            };
            if (result.Person.IsFaceFeatureCode)
            {
                var faceImage = await ReadFileData(parameter, iUserCode);
                if (faceImage != null)
                {
                    person.faceImage = Convert.ToBase64String(faceImage);
                }
            }
            if (result.Person.FingerprintFeatureCodeCout > 0)
            {
                person.fp = new string[result.Person.FingerprintFeatureCodeCout];
                for (int i = 0; i < result.Person.FingerprintFeatureCodeCout; i++)
                {
                    var feature = await ReadFeature(parameter, iUserCode, i);
                    if (feature != null)
                    {
                        person.fp[i] = Convert.ToBase64String(feature);
                    }
                }
            }
            return new FcardCommandResult
            {
                Data = person,
                Message = "查询成功",
                Status = CommandStatus.Succeed
            };

        }

        /// <summary>
        /// 获取所有人员
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> GetPersonAll(IFcardCommandParameter parameter)
        {
            var cmd = new ReadPersonDataBase(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = (ReadPersonDataBase_Result)cmd.getResult();

            var personList = new List<Models.PersonModel>();
            foreach (var p in result.PersonList)
            {
                var person = new Models.PersonModel
                {
                    name = p.PName,
                    cardData = p.CardData.ToString(),
                    cardStatus = p.CardStatus,
                    cardType = p.CardType,
                    code = p.PCode,
                    dept = p.Dept,
                    enterStatus = p.EnterStatus,
                    expiry = p.Expiry.ToString("yyyy-MM-dd HH:mm:ss"),
                    identity = p.Identity,
                    job = p.Job,
                    openTimes = p.OpenTimes,
                    password = p.Password,
                    timeGroup = p.TimeGroup,
                    userCode = (uint)p.UserCode,
                    face = p.IsFaceFeatureCode ? 1 : 0,
                    fpCount = p.FingerprintFeatureCodeCout
                };
                personList.Add(person);
            }
            return new FcardCommandResult
            {
                Data = personList,
                Message = "查询成功",
                Status = CommandStatus.Succeed
            };
        }
        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> ReadFace(IFcardCommandParameter parameter)
        {
            var data = JObject.Parse(parameter.Data.ToString());
            var d = new Dictionary<string, string>(data.ToObject<IDictionary<string, string>>(), StringComparer.CurrentCultureIgnoreCase);
            if (!uint.TryParse(d["usercode"], out var iUserCode))
            {
                return new FcardCommandResult
                {
                    Status = CommandStatus.ParameterError,
                    Message = "usercode error"
                };
            }
            return new FcardCommandResult
            {
                Data = await ReadFileData(parameter, iUserCode),
                Message = CommandStatus.Succeed.ToString(),
                Status = CommandStatus.Succeed
            };
        }


        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="iUserCode"></param>
        /// <param name="type"></param>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        private async Task<byte[]> ReadFileData(IFcardCommandParameter parameter, uint iUserCode)
        {
            DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFile cmd = new(parameter.CommandDetail,
                new DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFile_Parameter(iUserCode, 1, 1));
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFile_Result;
            return result.FileDatas;
        }
        /// <summary>
        /// 获取指纹特征
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> ReadFeature(IFcardCommandParameter parameter)
        {
            var data = JObject.Parse(parameter.Data.ToString());
            var d = new Dictionary<string, string>(data.ToObject<IDictionary<string, string>>(), StringComparer.CurrentCultureIgnoreCase);
            if (!uint.TryParse(d["usercode"], out var iUserCode))
            {
                return new FcardCommandResult
                {
                    Status = CommandStatus.ParameterError,
                    Message = "usercode error"
                };
            }
            if (!int.TryParse(d["serialNumber"], out var serialNumber) && serialNumber > 3)
            {
                return new FcardCommandResult
                {
                    Status = CommandStatus.ParameterError,
                    Message = "serialNumber error"
                };
            }
            return new FcardCommandResult
            {
                Data = await ReadFeature(parameter, iUserCode, serialNumber),
                Message = CommandStatus.Succeed.ToString(),
                Status = CommandStatus.Succeed
            };
        }
        /// <summary>
        /// 读取指纹特征码
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="iUserCode"></param>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        private async Task<byte[]> ReadFeature(IFcardCommandParameter parameter, uint iUserCode, int serialNumber)
        {
            DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFeatureCode cmd = new(parameter.CommandDetail,
                new DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFeatureCode_Parameter(iUserCode, 2, serialNumber));
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFeatureCode_Result;
            return result.FileDatas;
        }
        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> GetRecord(IFcardCommandParameter parameter)
        {
            int quantity = 60;
            try
            {
                if (!string.IsNullOrWhiteSpace(parameter.Data))
                    quantity = (int)JObject.Parse(parameter.Data)["quantity"];
            }
            catch
            {
            }

            var cmd = new ReadTransactionAndImageDatabase(parameter.CommandDetail, new ReadTransactionAndImageDatabase_Parameter(quantity, false, null)
            {
                AutoDownloadImage = MyRegistry.Options.RecordImage
            });
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as ReadTransactionAndImageDatabase_Result;
            var tai = new Models.TransactionAndImage();
            tai.Quantity = result.Quantity;
            tai.Readable = result.readable;
            tai.RecordList = new List<Models.FaceTransaction>();
            foreach (var item in result.TransactionList)
            {
                tai.RecordList.Add(new Models.FaceTransaction
                {
                    UserCode = item.UserCode,
                    Accesstype = item.Accesstype,
                    BodyTemperature = ((double)item.BodyTemperature / 10),
                    Photo = item.Photo,
                    RecordDate = item.TransactionDate.ToDateTimeStr(),
                    RecordImage = item.PhotoDataBuf,
                    RecordNumber = item.SerialNumber,
                    RecordType = item.TransactionType,
                    RecordMsg = MessageType.TransactionCodeNameList[item.TransactionType][item.TransactionCode]
                });
            }
            return new FcardCommandResult
            {
                Data = tai,
                Message = "读取记录成功",
                Status = CommandStatus.Succeed
            };
        }
        public async Task<IFcardCommandResult> GetRecordByIndex(IFcardCommandParameter parameter)
        {
            GetRecordByIndexModel model;
            try
            {
                model = JsonConvert.DeserializeObject<GetRecordByIndexModel>(parameter.Data);
            }
            catch
            {
                return new FcardCommandResult
                {
                    Message = "Parameter Error",
                    Status = CommandStatus.ParameterError
                };
            }
            if (model.Quantity > 60)
                model.Quantity = 60;
            if (model.ReadIndex == 0)
            {
                return new FcardCommandResult
                {
                    Message = "ReadIndex is 0",
                    Status = CommandStatus.ParameterError
                };
            }
            if (model.TransactionType <= 0 || model.TransactionType > 4)
            {
                return new FcardCommandResult
                {
                    Message = "TransactionType data range 1-4",
                    Status = CommandStatus.ParameterError
                };
            }
            var par = new ReadTransactionDatabaseByIndex_Parameter(model.TransactionType, model.ReadIndex, model.Quantity);
            var cmd = new ReadTransactionDatabaseByIndex(parameter.CommandDetail, par);

            await parameter.Allocator.AddCommandAsync(cmd);
            var result = (DoNetDrive.Protocol.Door.Door8800.Transaction.ReadTransactionDatabaseByIndex_Result)cmd.getResult();
            var recordList = new List<Models.FaceTransaction>();
            foreach (var item in result.TransactionList)
            {
                FaceTransaction transaction;
                if (result.TransactionType == DoNetDrive.Protocol.Door.Door8800.Transaction.e_TransactionDatabaseType.OnCardTransaction)
                {
                    var cardTransaction = (CardTransaction)item;
                    transaction = new FaceTransaction
                    {
                        Accesstype = cardTransaction.Accesstype,
                        Door = 1,
                        Photo = cardTransaction.Photo,
                        RecordCode = cardTransaction.TransactionCode,
                        RecordNumber = cardTransaction.SerialNumber,
                        RecordDate = cardTransaction.TransactionDate.ToDateTimeStr(),
                        RecordType = cardTransaction.TransactionType,
                        RecordMsg = MessageType.TransactionCodeNameList[item.TransactionType][item.TransactionCode],
                        UserCode = cardTransaction.UserCode,
                        SN = parameter.Sn
                    };
                }
                else
                {
                    var systemTransaction = (SystemTransaction)item;
                    transaction = new FaceTransaction
                    {
                        Door = systemTransaction.Door,
                        RecordCode = systemTransaction.TransactionCode,
                        RecordNumber = systemTransaction.SerialNumber,
                        RecordDate = systemTransaction.TransactionDate.ToDateTimeStr(),
                        RecordType = systemTransaction.TransactionType,
                        RecordMsg = MessageType.TransactionCodeNameList[item.TransactionType][item.TransactionCode],
                        SN = parameter.Sn
                    };
                }
                recordList.Add(transaction);
            }
            if (MyRegistry.Options.RecordImage)
            {
                foreach (var record in recordList)
                {
                    if (record.Photo == 1)
                    {
                        record.RecordImage = await ReadFile(parameter, (uint)record.RecordNumber, 3, 1);
                    }
                }
            }
           
            return new FcardCommandResult
            {
                Message = "ok",
                Status = CommandStatus.Succeed,
                Data = recordList
            };
        }

        private async Task<byte[]> ReadFile(IFcardCommandParameter parameter, uint iUserCode, int type, int serialNumber)
        {
            var par = new ReadFile_Parameter(iUserCode, type, serialNumber);
            var cmd = new ReadFile(parameter.CommandDetail, par);
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = (ReadFile_Result)cmd.getResult();
            return result.FileDatas;
        }



        /// <summary>
        /// 门常开
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> HoldDoor(IFcardCommandParameter parameter)
        {
            HoldDoor cmd = new(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "门常开成功"
            };
        }
        /// <summary>
        /// 远程开门
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> OpenDoor(IFcardCommandParameter parameter)
        {
            OpenDoor cmd = new(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "开门成功"
            };
        }
        /// <summary>
        /// 修复记录
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> ResetRecord(IFcardCommandParameter parameter)
        {
            var par = new WriteTransactionDatabaseReadIndex_Parameter(e_TransactionDatabaseType.OnCardTransaction, 0);
            var cmd = new WriteTransactionDatabaseReadIndex(parameter.CommandDetail, par);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Status = CommandStatus.Succeed,
                Message = "修复记录成功"
            };
        }
        /// <summary>
        /// 读取工作参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<IFcardCommandResult> GetWorkParam(IFcardCommandParameter parameter)
        {
            Models.WorkParameter work = new Models.WorkParameter();

            var readOEM = new ReadOEM(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readOEM);
            var oem = readOEM.getResult() as OEM_Result;
            work.Maker = oem.Detail;

            var readDriveLanguage = new ReadDriveLanguage(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readDriveLanguage);
            var language = readDriveLanguage.getResult() as ReadDriveLanguage_Result;
            work.Language = (byte)language.Language;

            var readDriveVolume = new ReadDriveVolume(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readDriveVolume);
            var volume = readDriveVolume.getResult() as ReadDriveVolume_Result;
            work.Volume = (byte)volume.Volume;

            var readManageMenuPassword = new ReadManageMenuPassword(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readManageMenuPassword);
            var password = readManageMenuPassword.getResult() as ReadManageMenuPassword_Result;
            work.MenuPassword = password.Password;

            var readSaveRecordImage = new ReadSaveRecordImage(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readSaveRecordImage);
            var saveImage = readSaveRecordImage.getResult() as ReadSaveRecordImage_Result;
            work.SavePhoto = (byte)(saveImage.SaveImageSwitch ? 1 : 0);

            var readWatchState = new ReadWatchState(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readWatchState);
            work.MsgPush = readWatchState.WatchState;

            var readTime = new ReadTime(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readTime);
            var time = readTime.getResult() as ReadTime_Result;
            work.Time = time.ControllerDate;

            var readLocalIdentity = new ReadLocalIdentity(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(readLocalIdentity);
            var identity = readLocalIdentity.getResult() as ReadLocalIdentity_Result;
            work.Name = identity.LocalName;
            work.Door = identity.InOut;

            return new FcardCommandResult
            {
                Data = work,
                Message = "查询成功",
                Status = CommandStatus.Succeed
            };
        }
        /// <summary>
        /// 设置工作参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<IFcardCommandResult> SetWorkParam(IFcardCommandParameter parameter)
        {
            var par = JsonConvert.DeserializeObject<Models.WorkParameter>(parameter.Data);
            var check = CheckWorkParameter(par);
            if (!check.Item1)
            {
                return new FcardCommandResult
                {
                    Message = check.Item2,
                    Status = CommandStatus.PasswordError
                };
            }
            if (par.Name != null && par.Door != null)
            {
                WriteLocalIdentity cmd = new WriteLocalIdentity(parameter.CommandDetail, new WriteLocalIdentity_Parameter(1, par.Name, (byte)par.Door));
                await parameter.Allocator.AddCommandAsync(cmd);
            }

            if (par.Maker != null)
            {
                WriteOEM cmd = new WriteOEM(parameter.CommandDetail, new OEM_Parameter(par.Maker));
                await parameter.Allocator.AddCommandAsync(cmd);
            }

            if (par.Language != null)
            {
                WriteDriveLanguage cmd = new WriteDriveLanguage(parameter.CommandDetail, new WriteDriveLanguage_Parameter((int)par.Language));
                await parameter.Allocator.AddCommandAsync(cmd);
            }

            if (par.Volume != null)
            {
                WriteDriveVolume cmd = new WriteDriveVolume(parameter.CommandDetail, new WriteDriveVolume_Parameter((int)par.Volume));
                await parameter.Allocator.AddCommandAsync(cmd);
            }

            if (par.MenuPassword != null)
            {
                WriteManageMenuPassword cmd = new WriteManageMenuPassword(parameter.CommandDetail, new WriteManageMenuPassword_Parameter(par.MenuPassword));
                await parameter.Allocator.AddCommandAsync(cmd);
            }

            if (par.SavePhoto != null)
            {
                WriteSaveRecordImage cmd = new WriteSaveRecordImage(parameter.CommandDetail, new WriteSaveRecordImage_Parameter(par.SavePhoto == 1));
                await parameter.Allocator.AddCommandAsync(cmd);
            }

            if (par.MsgPush != null)
            {
                if (par.MsgPush == 0)
                {
                    CloseWatch cmd = new CloseWatch(parameter.CommandDetail);
                    await parameter.Allocator.AddCommandAsync(cmd);
                }
                else
                {
                    BeginWatch cmd = new BeginWatch(parameter.CommandDetail);
                    await parameter.Allocator.AddCommandAsync(cmd);
                }
            }
            if (par.Time != null)
            {
                WriteCustomTime cmd = new WriteCustomTime(parameter.CommandDetail, new WriteCustomTime_Parameter((DateTime)par.Time));
                await parameter.Allocator.AddCommandAsync(cmd);
            }
            return new FcardCommandResult
            {
                Message = "设置成功",
                Status = CommandStatus.Succeed
            };
        }

        public (bool, string) CheckWorkParameter(Models.WorkParameter parameter)
        {
            (bool, string) result;
            result.Item1 = false;
            result.Item2 = String.Empty;
            if (parameter == null)
            {
                result.Item2 = "参数错误，请检查参数格式";
                return result;
            }
            if (parameter.Name == null &&
                parameter.Door == null &&
                parameter.Maker == null &&
                parameter.Language == null &&
                parameter.Volume == null &&
                parameter.MenuPassword == null &&
                parameter.SavePhoto == null &&
                parameter.MsgPush == null &&
                parameter.Time == null)
            {
                result.Item2 = "参数错误，请检查参数格式";
                return result;
            }
            if (parameter.Maker != null)
            {
                if (parameter.Maker.Manufacturer == null || parameter.Maker.WebAddr == null || parameter.Maker.DeliveryDate == DateTime.MinValue)
                {
                    result.Item2 = "生产制造商信息错误";
                    return result;
                }

            }
            if (parameter.Name != null && parameter.Name.Length > 30)
            {
                result.Item2 = "设备名称信息错误:设备名称不能超过30个文字";
                return result;
            }
            if (parameter.Door != null && (parameter.Door > 1 || parameter.Door < 0))
            {
                result.Item2 = "设备进出方向错误：取值返回0-1";
                return result;
            }
            if ((parameter.Name != null && parameter.Door == null) || (parameter.Name == null && parameter.Door != null))
            {
                result.Item2 = "设备名称或进出方向两者必须同时填写";
                return result;
            }
            if (parameter.Maker != null)
            {
                var maker = parameter.Maker;
                if (maker.Manufacturer != null && maker.Manufacturer.Length > 30)
                {
                    result.Item2 = "生产制造商名称错误：生产制造商名称不能超过30个文字";
                    return result;
                }
                if (maker.WebAddr != null && maker.WebAddr.Length > 60)
                {
                    result.Item2 = "制造商网址错误：制造商网址不能超过60个文字";
                    return result;
                }
            }
            if (parameter.Language != null && (parameter.Language > 16 || parameter.Language < 1))
            {
                result.Item2 = "语言类型错误：取值范围 1-16";
                return result;
            }
            if (parameter.Volume != null && (parameter.Volume > 10 || parameter.Volume < 0))
            {
                result.Item2 = "音量错误：取值范围 0-10";
                return result;
            }
            if (parameter.MenuPassword != null &&
                (parameter.MenuPassword.Length > 8 || parameter.MenuPassword.Length < 4) &&
                int.TryParse(parameter.MenuPassword, out _))
            {
                result.Item2 = "菜单密码错误：仅支持4-8位数字密码";
                return result;
            }
            if (parameter.SavePhoto != null && (parameter.SavePhoto > 1 || parameter.SavePhoto < 0))
            {
                result.Item2 = "现场照片保存开关错误：取值返回0-1";
                return result;
            }
            if (parameter.MsgPush != null && parameter.MsgPush > 1 || parameter.MsgPush < 0)
            {
                result.Item2 = "消息推送开关错误：取值返回0-1";
                return result;
            }
            var maxDateTime = DateTime.Parse("2088-12-30");
            var minDateTime = DateTime.Parse("2000-01-01");
            if (parameter.Time != null && (parameter.Time < minDateTime || parameter.Time > maxDateTime))
            {
                result.Item2 = "日期时间同步错误：日期时间不能小于2000年01月01日并且不能大于2088-12-30";
                return result;
            }
            result.Item1 = true;
            return result;
        }

        public async Task<IFcardCommandResult> CommandTest(IFcardCommandParameter parameter)
        {
            DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFile cmd = new(parameter.CommandDetail,
                new DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFile_Parameter(10, 3, 1));

            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as DoNetDrive.Protocol.Fingerprint.AdditionalData.ReadFile_Result;
            return new FcardCommandResult
            {
                Data = result,
                Status = CommandStatus.Succeed
            };


        }

        /// <summary>
        /// 写入服务器参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> WriteNetworkServer(IFcardCommandParameter parameter)
        {
            var serverDetail = JsonConvert.DeserializeObject<Models.NetworkServerDetail>(parameter.Data);
            var par = new WriteNetworkServerDetail_Parameter(serverDetail.Prot, serverDetail.IP);
            par.ServerDomain = serverDetail.Domain;
            var cmd = new WriteNetworkServerDetail(parameter.CommandDetail, par);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Message = "设置成功",
                Status = CommandStatus.Succeed
            };
        }

        /// <summary>
        /// 同步时间
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<IFcardCommandResult> SyncDateTime(IFcardCommandParameter parameter)
        {
            DateTime dateTime = DateTime.Now;
            try
            {
                if (!string.IsNullOrWhiteSpace(parameter.Data))
                    dateTime = Convert.ToDateTime(JObject.Parse(parameter.Data)["dateTime"]);
            }
            catch
            {
            }
            var cmd = new WriteCustomTime(parameter.CommandDetail, new WriteCustomTime_Parameter(dateTime));
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Message = CommandStatus.Succeed.ToString(),
                Status = CommandStatus.Succeed
            };
        }


        public async Task<IFcardCommandResult> WriteTimeGroup(IFcardCommandParameter parameter)
        {
            var result = TryGetWeekTimeGroups(parameter, out var weekTimeGroups);
            if (result.Item1 == false)
            {
                return new FcardCommandResult
                {
                    Message = result.Item2,
                    Status = CommandStatus.ParameterError
                };
            }
            var par = new AddTimeGroup_Parameter(weekTimeGroups);
            var cmd = new AddTimeGroup(parameter.CommandDetail, par);
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult
            {
                Message = "设置成功",
                Status = CommandStatus.Succeed
            };
        }

        private (bool, string) TryGetWeekTimeGroups(IFcardCommandParameter parameter, out List<WeekTimeGroup> weekTimeGroups)
        {
            weekTimeGroups = new List<WeekTimeGroup>();
            var result = (false, string.Empty);
            var list = JsonConvert.DeserializeObject<List<Models.TimeGroupModel>>(parameter.Data);
            if (!list.Any())
            {
                result.Item2 = "参数未填写";
                return result;
            }
            if (list.Count > 64)
            {
                result.Item2 = "开门时段列表最大只能64";
                return result;
            }
            foreach (var weekTimGroup in list)
            {
                if (weekTimGroup.Index > 64 || weekTimGroup.Index == 0)
                {
                    result.Item2 = "Index 取值范围1-64";
                    return result;
                }
                if (weekTimGroup.DayTimeList.Count != 7)
                {
                    result.Item2 = "DayTimeList 必须是一周的时间（7天）";
                    return result;
                }
                WeekTimeGroup timeGroup = new WeekTimeGroup(8);
                timeGroup.SetIndex(weekTimGroup.Index);
                for (int i = 0; i < 7; i++)
                {
                    var dayTime = weekTimGroup.DayTimeList[i];
                    var dayTimeGroup = timeGroup.GetItem((int)dayTime.DayWeek);
                    result = TryGetDayTimeGroup(dayTime, dayTimeGroup);
                    if (result.Item1 == false)
                        return result;
                }
                weekTimeGroups.Add(timeGroup);
            }
            return result;
        }

        private (bool, string) TryGetDayTimeGroup(DayTimeModel dayTime, DayTimeGroup dayTimeGroup)
        {
            var result = (false, string.Empty);
            int index = 0;
            try
            {
                int count = dayTimeGroup.GetSegmentCount();
                for (int i = 0; i < count; i++)
                {
                    var segments = dayTimeGroup.GetItem(i);
                    if (i < dayTime.TimeSegmentList.Count)
                    {
                        var begins = dayTime.TimeSegmentList[i].Begin.Split(':');
                        segments.SetBeginTime(int.Parse(begins[0]), int.Parse(begins[1]));
                        var ends = dayTime.TimeSegmentList[i].End.Split(':');
                        segments.SetEndTime(int.Parse(ends[0]), int.Parse(ends[1]));
                    }
                    else
                    {
                        segments.SetBeginTime(0, 0);
                        segments.SetEndTime(0, 0);
                    }
                    index = i;
                }
                result.Item1 = true;
            }
            catch
            {
                result.Item2 = $" DayTimeList DayWeek{dayTime.DayWeek}, TimeSegmentList week index={index} error";
            }
            return result;
        }

        public async Task<IFcardCommandResult> ReadTimeGroup(IFcardCommandParameter parameter)
        {
            ReadTimeGroup cmd = new ReadTimeGroup(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as ReadTimeGroup_Result;
            List<Models.TimeGroupModel> timeGroupModels = new List<Models.TimeGroupModel>();
            int Index = 1;
            foreach (var item in result.ListWeekTimeGroup)
            {
                var model = new TimeGroupModel();
                model.Index = Index++;
                model.DayTimeList = new List<DayTimeModel>();
                for (int i = 0; i < 7; i++)
                {
                    var day = item.GetItem(i);
                    var dModel = new DayTimeModel()
                    {
                        DayWeek = (DayOfWeek)i,
                        TimeSegmentList = new List<Models.TimeSegment>()
                    };
                    var count = day.GetSegmentCount();
                    for (int j = 0; j < count; j++)
                    {
                        Models.TimeSegment segment = new Models.TimeSegment()
                        {
                            Begin = day.GetItem(j).GetBeginTime().ToString("HH:mm"),
                            End = day.GetItem(j).GetEndTime().ToString("HH:mm"),
                        };
                        dModel.TimeSegmentList.Add(segment);
                    }
                    model.DayTimeList.Add(dModel);
                }
                timeGroupModels.Add(model);
            }
            return new FcardCommandResult
            {
                Message = "获取成功",
                Status = CommandStatus.Succeed,
                Data = timeGroupModels
            };
        }


        public async Task<IFcardCommandResult> ReadKeepAlive(IFcardCommandParameter parameter)
        {
            ReadKeepAliveInterval cmd = new ReadKeepAliveInterval(parameter.CommandDetail);
            await parameter.Allocator.AddCommandAsync(cmd);
            var result = cmd.getResult() as ReadKeepAliveInterval_Result;
            return new FcardCommandResult
            {
                Message = "获取成功",
                Status = CommandStatus.Succeed,
                Data = result.IntervalTime
            };
        }

        public async Task<IFcardCommandResult> WriteKeepAlive(IFcardCommandParameter parameter)
        {
            var apiResult = new FcardCommandResult
            {
                Message = "intervalTime The value must be greater than 0 and less than 65535",
                Status = CommandStatus.ParameterError
            };
            try
            {
                var intervalTime = ushort.Parse(JObject.Parse(parameter.Data)["intervalTime"].ToString());
                if (intervalTime <= 0 || intervalTime > 65535)
                {
                    return apiResult;
                }
                WriteKeepAliveInterval cmd = new WriteKeepAliveInterval(parameter.CommandDetail, new WriteKeepAliveInterval_Parameter(intervalTime));
                await parameter.Allocator.AddCommandAsync(cmd);
                return new FcardCommandResult
                {
                    Message = "获取成功",
                    Status = CommandStatus.Succeed
                };
            }
            catch
            {
                return apiResult;
            }
        }

        //public async Task<IFcardCommandResult> DeleteFeatureCode(IFcardCommandParameter parameter)
        //{
        //    var model = JsonConvert.DeserializeObject<DeleteFeatureCodeModel>(parameter.Data);
        //    if (model == null)
        //    {
        //        return new FcardCommandResult()
        //        {
        //            Status = CommandStatus.ParameterError,
        //            Message = "获取参数错误"
        //        };
        //    }
        //    var par = GetDeleteFeatureCodeParameter(model, out var result);
        //    if (result.Status == CommandStatus.PasswordError)
        //    {
        //        return result;
        //    }
        //    var isOk = await DeleteFeatureCode(parameter, par);
        //    result.Message = isOk ? "删除完成" : "删除失败";
        //    result.Status = isOk ? CommandStatus.Succeed : CommandStatus.SystemError;
        //    return result;
        //}
        /// <summary>
        /// 删除特征
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="par"></param>
        /// <returns></returns>
        private async Task<bool> DeleteFeatureCode(IFcardCommandParameter parameter, DeleteFeatureCode_Parameter par)
        {
            try
            {
                var cmd = new DeleteFeatureCode(parameter.CommandDetail, par);
                await parameter.Allocator.AddCommandAsync(cmd);
                return true;
            }
            catch (Exception ex)
            { }
            return false;
        }
        /// <summary>
        /// 获取删除特征参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private DeleteFeatureCode_Parameter? GetDeleteFeatureCodeParameter(DeleteFeatureCodeModel model, out FcardCommandResult result)
        {
            DeleteFeatureCode_Parameter par = null;
            result = new FcardCommandResult()
            {
                Status = CommandStatus.ParameterError,
                Message = "获取参数错误"
            };

            byte[] fingerprintList = new byte[10];
            if (model.Fingerprint != null)
            {
                foreach (var item in model.Fingerprint)
                {
                    if (item > 9 || item < 0)
                    {
                        result.Message = "Fingerprint 取值范围为：0-9";
                        return par;
                    }
                    fingerprintList[item] = 1;
                }

            }
            byte[] palmList = new byte[2];//掌静脉特征码
            if (model.Palm != null)
            {
                foreach (var item in model.Palm)
                {
                    if (item > 2 || item < 1)
                    {
                        result.Message = "Palm 取值范围为：1-2";
                        return par;
                    }
                    palmList[item - 1] = 1;
                }
            }
            byte[] photoList = new byte[5];
            bool photo = false;
            if (model.Photo != null)
            {
                foreach (var item in model.Photo)
                {
                    if (item > 5 || item < 1)
                    {
                        result.Message = "Photo 取值范围为：1-5";
                        return par;
                    }
                    photoList[item - 1] = 1;
                }
                photo = true;
            }
            par = new DeleteFeatureCode_Parameter(model.UserCode, photoList, fingerprintList, photo);
            return par;
        }

        public async Task<IFcardCommandResult> DeleteFeatureCode(IFcardCommandParameter parameter)
        {
            var modelList = JsonConvert.DeserializeObject<List<DeleteFeatureCodeModel>>(parameter.Data);
            if (modelList == null)
            {
                return new FcardCommandResult()
                {
                    Status = CommandStatus.ParameterError,
                    Message = "获取参数错误"
                };
            }
            var allResult = new List<object>();
            foreach (var item in modelList)
            {
                var par = GetDeleteFeatureCodeParameter(item, out var result);
                if (result.Status == CommandStatus.PasswordError)
                {
                    allResult.Add(new
                    {
                        item.UserCode,
                        result.Message
                    });
                    continue;
                }
                var isOk = await DeleteFeatureCode(parameter, par);
                if (isOk == false)
                {
                    allResult.Add(new
                    {
                        item.UserCode,
                        Message = "删除失败"
                    });
                }
            }
            return new FcardCommandResult
            {
                Data = allResult,
                Message = "删除完成",
                Status = CommandStatus.Succeed
            }; ;
        }

        public async Task<IFcardCommandResult> CloseAlarm(IFcardCommandParameter parameter)
        {
            CloseAlarmModel model;
            try
            {
                model = JsonConvert.DeserializeObject<CloseAlarmModel>(parameter.Data);
            }
            catch
            {

                return new FcardCommandResult()
                {
                    Status = CommandStatus.ParameterError,
                    Message = "Parameter Error"
                };
            }

            byte[] list = new byte[7];
            list[0] = Convert.ToByte(model.IllegalVerificationAlarm ? 1 : 0);
            list[1] = Convert.ToByte(model.DoorMagneticAlarm ? 1 : 0);
            list[2] = Convert.ToByte(model.PasswordAlarm ? 1 : 0);
            list[3] = Convert.ToByte(model.OpenDoorTimeoutAlarm ? 1 : 0);
            list[4] = Convert.ToByte(model.BlacklistAlarm ? 1 : 0);
            list[5] = Convert.ToByte(model.AntiDisassemblyAlarm ? 1 : 0);
            list[6] = Convert.ToByte(model.FireAlarm ? 1 : 0);

            var par = new CloseAlarm_Parameter(list);
            var write = new CloseAlarm(parameter.CommandDetail, par);
            await parameter.Allocator.AddCommandAsync(write);
            return new FcardCommandResult()
            {
                Status = CommandStatus.Succeed,
                Message = CommandStatus.Succeed.ToString()
            };
        }

        public async Task<IFcardCommandResult> WriteDateTime(IFcardCommandParameter parameter)
        {
            WriteDateTimeModel model;
            try
            {
                model = JsonConvert.DeserializeObject<WriteDateTimeModel>(parameter.Data);
            }
            catch
            {

                return new FcardCommandResult()
                {
                    Status = CommandStatus.ParameterError,
                    Message = "Parameter Error"
                };
            }
            var cmd = new WriteCustomTime(parameter.CommandDetail, new WriteCustomTime_Parameter(model.DateTime));
            await parameter.Allocator.AddCommandAsync(cmd);
            return new FcardCommandResult()
            {
                Status = CommandStatus.Succeed,
                Message = CommandStatus.Succeed.ToString()
            };
        }

        public async Task<IFcardCommandResult> ClearRecord(IFcardCommandParameter parameter)
        {
            var values = Enum.GetValues(typeof(e_TransactionDatabaseType));
            foreach (e_TransactionDatabaseType item in values)
            {
                var cmd = new ClearTransactionDatabase(parameter.CommandDetail, new ClearTransactionDatabase_Parameter(item));
                await parameter.Allocator.AddCommandAsync(cmd);
            }
            return new FcardCommandResult()
            {
                Status = CommandStatus.Succeed,
                Message = CommandStatus.Succeed.ToString()
            };
        }

    }
}
