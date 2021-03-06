﻿#region Apache License Version 2.0
/*----------------------------------------------------------------

Copyright 2017 Jeffrey Su & Suzhou Senparc Network Technology Co.,Ltd.

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
except in compliance with the License. You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the
License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
either express or implied. See the License for the specific language governing permissions
and limitations under the License.

Detail: https://github.com/JeffreySu/WeiXinMPSDK/blob/master/license.md

----------------------------------------------------------------*/
#endregion Apache License Version 2.0

/*----------------------------------------------------------------
    Copyright (C) 2017 Senparc
    
    文件名：EntityHelper.cs
    文件功能描述：实体与xml相互转换
    
    
    创建标识：Senparc - 20150211
    
    修改标识：Senparc - 20150303
    修改描述：整理接口
    
    修改标识：Senparc - 20170810
    修改描述：v14.5.9 提取EntityHelper.FillClassValue()方法，优化FillEntityWithXml()方法
----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Senparc.Weixin.Helpers;
using Senparc.Weixin.MP.Entities;
using Senparc.Weixin.Utilities;
using System.Web;

namespace Senparc.Weixin.MP.Helpers
{
    /// <summary>
    /// 实体帮助类
    /// </summary>
    public static class EntityHelper
    {
        /// <summary>
        /// 根据XML信息填充实实体
        /// </summary>
        /// <typeparam name="T">MessageBase为基类的类型，Response和Request都可以</typeparam>
        /// <param name="entity">实体</param>
        /// <param name="doc">XML</param>
        public static void FillEntityWithXml<T>(this T entity, XDocument doc) where T : /*MessageBase*/ class, new()
        {
            //如果实体为空，生成一个新的对象
            entity = entity ?? new T();
            var root = doc.Root;
            if (root == null)
            {
                return;//无法处理
            }
            //{2017-9-27测试时使用，将接收到的XML文件保存
            string Path = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            Senparc.Weixin.WeixinTrace.SendCustomLog("调试程序", Path);
            doc.Save(Path + "App_Data/XML/" + DateTime.Now.ToString("d_MMM_yyyy_HH_mm_ss") + "_Request_" + root.Element("FromUserName").Value + DateTime.Now.Ticks+".txt");//测试时可开启，帮助跟踪数据
           //}
            //取得实体对象的所有属性
            var props = entity.GetType().GetProperties();
            foreach (var prop in props)
            {
                var propName = prop.Name;                //取得属性的名称  类似 MsgType、MsgId、ToUserName、FromUserName等

                if (root.Element(propName) != null)
                {//在XML文件中存在该节点
                    switch (prop.PropertyType.Name)
                    {//分析该属性的数据类型   并对该属性进行填充  （XML内容全部为字符形式的，需要转换到不同的数据类型）
                        //case "String":
                        //    goto default;
                        case "DateTime":
                        case "Int32":
                        case "Int64":
                        case "Double":
                        case "Nullable`1": //可为空对象
                            EntityUtility.EntityUtility.FillSystemType(entity, prop, root.Element(propName).Value);    //向实体对象的属性填充值
                            break;
                        case "Boolean":
                            if (propName == "FuncFlag")
                            {//该属性在C#中为Boolean类型，在XML中表现为整形，需要在此处预处理为Boolean类型再进行填充
                                EntityUtility.EntityUtility.FillSystemType(entity, prop, root.Element(propName).Value == "1");
                            }
                            else
                            {
                                goto default;
                            }
                            break;

                        //以下为枚举类型
                        case "RequestMsgType":
                            //已设为只读
                            //prop.SetValue(entity, MsgTypeHelper.GetRequestMsgType(root.Element(propName).Value), null);
                            break;
                        case "ResponseMsgType": //Response适用
                            //已设为只读
                            //prop.SetValue(entity, MsgTypeHelper.GetResponseMsgType(root.Element(propName).Value), null);
                            break;
                        case "Event":
                            //已设为只读
                            //prop.SetValue(entity, EventHelper.GetEventType(root.Element(propName).Value), null);
                            break;
                        //以下为实体类型
                        case "List`1": //List<T>类型，ResponseMessageNews适用
                            {
                                var genericArguments = prop.PropertyType.GetGenericArguments();     //取得泛型参数列表
                                var genericArgumentTypeName = genericArguments[0].Name;              //取得第一个参数的类型
                          
                                if (genericArgumentTypeName == "Article")                                            //参数类型为Article类型（图文）
                                {
                                    //文章下属节点item
                                    List<Article> articles = new List<Article>();                                         //生成图文列表对象
                                    foreach (var item in root.Element(propName).Elements("item"))        //取得XML文件中“Articles”节点下的所有item
                                    {
                                        var article = new Article();                                                                //生成单个图文对象
                                        FillEntityWithXml(article, new XDocument(item));                          //使用取得XML内容填充该图文对象
                                        articles.Add(article);                                                                         //该图文对象加入到图文列表对象中
                                    }
                                    prop.SetValue(entity, articles, null);                                                     //设置实体的该属性---  Articles属性 
                                }
                                else if (genericArgumentTypeName == "Account")                              // Account类型与响应回复多客服消息  ResponseMessageTransfer_Customer_Service中定义的数据类型不一致
                                {                                                                                                              //有可能永远不会执行
                                    List<CustomerServiceAccount> accounts = new List<CustomerServiceAccount>();   
                                    foreach (var item in root.Elements(propName))
                                    {
                                        var account = new CustomerServiceAccount();
                                        FillEntityWithXml(account, new XDocument(item));
                                        accounts.Add(account);
                                    }
                                    prop.SetValue(entity, accounts, null);
                                }
                                else if (genericArgumentTypeName == "PicItem")                                //参数类型为图片---此处程序段由填充  SendPicsInfo属性时调用
                                {
                                    List<PicItem> picItems = new List<PicItem>();
                                    foreach (var item in root.Elements(propName).Elements("item"))    
                                    {
                                        var picItem = new PicItem();                                                           //生成图片对象
                                        var picMd5Sum = item.Element("PicMd5Sum").Value;                  //取出XML此节点下的PicMd5Sum数据
                                        Md5Sum md5Sum = new Md5Sum() { PicMd5Sum = picMd5Sum };  //生成Md5Sum（图片的MD5值）对象
                                        picItem.item = md5Sum;
                                        picItems.Add(picItem);
                                    }
                                    prop.SetValue(entity, picItems, null);
                                }
                                else if (genericArgumentTypeName == "AroundBeacon")                    //参数类型为AroundBeacon类型（事件之摇一摇事件通知）            
                                {
                                    List<AroundBeacon> aroundBeacons = new List<AroundBeacon>();
                                    foreach (var item in root.Elements(propName).Elements("AroundBeacon"))
                                    {
                                        var aroundBeaconItem = new AroundBeacon();
                                        FillEntityWithXml(aroundBeaconItem, new XDocument(item));
                                        aroundBeacons.Add(aroundBeaconItem);
                                    }
                                    prop.SetValue(entity, aroundBeacons, null);
                                }
                                else if (genericArgumentTypeName == "CopyrightCheckResult_ResultList")//参数类型为Article类型（事件之推送群发结果）  RequestMessageEvent_MassSendJobFinish
                                {                                                                                                                        //---此处程序段由填充  CopyrightCheckResult属性时调用
                                    List<CopyrightCheckResult_ResultList> resultList = new List<CopyrightCheckResult_ResultList>();
                                    foreach (var item in root.Elements("ResultList").Elements("item"))            //比较特殊,又嵌套了一个ResultList
                                    {
                                        CopyrightCheckResult_ResultList resultItem = new CopyrightCheckResult_ResultList();
                                        FillEntityWithXml(resultItem.item, new XDocument(item));                   //此处嵌套调用,填充  CopyrightCheckResult_ResultList_Item类型数据
                                        resultList.Add(resultItem);
                                    }
                                    prop.SetValue(entity, resultList, null);
                                }
                                break;
                            }
                        case "Music"://ResponseMessageMusic适用
                            FillClassValue<Music>(entity, root, propName, prop);
                            break;
                        case "Image"://ResponseMessageImage适用
                            FillClassValue<Image>(entity, root, propName, prop);
                            break;
                        case "Voice"://ResponseMessageVoice适用
                            FillClassValue<Voice>(entity, root, propName, prop);
                            break;
                        case "Video"://ResponseMessageVideo适用
                            FillClassValue<Video>(entity, root, propName, prop);
                            break;
                        case "ScanCodeInfo"://扫码事件中的ScanCodeInfo适用
                            FillClassValue<ScanCodeInfo>(entity, root, propName, prop);
                            break;
                        case "SendLocationInfo"://弹出地理位置选择器的事件推送中的SendLocationInfo适用
                            FillClassValue<SendLocationInfo>(entity, root, propName, prop);
                            break;
                        case "SendPicsInfo"://系统拍照发图中的SendPicsInfo适用
                            FillClassValue<SendPicsInfo>(entity, root, propName, prop);
                            break;
                        case "ChosenBeacon"://摇一摇事件通知
                            FillClassValue<ChosenBeacon>(entity, root, propName, prop);
                            break;
                        case "AroundBeacon"://摇一摇事件通知
                            FillClassValue<AroundBeacon>(entity, root, propName, prop);
                            break;

                        #region RequestMessageEvent_MassSendJobFinish
                        case "CopyrightCheckResult":
                            FillClassValue<CopyrightCheckResult>(entity, root, propName, prop);
                            break;
                        case "CopyrightCheckResult_ResultList_Item":
                            FillClassValue<CopyrightCheckResult_ResultList_Item>(entity, root, "item", prop);
                            break;
                        #endregion

                        default:
                            prop.SetValue(entity, root.Element(propName).Value, null);
                            break;
                    }
                }
               
            }
        }

        /// <summary>
        /// 填充复杂类型的参数
        /// </summary>
        /// <typeparam name="T">复杂类型</typeparam>
        /// <param name="entity">被填充实体</param>
        /// <param name="root">XML节点</param>
        /// <param name="childElementName">XML下一级节点的名称</param>
        /// <param name="prop">属性对象</param>
        public static void FillClassValue<T>(object entity, XElement root, string childElementName, PropertyInfo prop)
            where T : /*MessageBase*/ class, new()
        {
            T subType = new T();
            FillEntityWithXml(subType, new XDocument(root.Element(childElementName)));
            prop.SetValue(entity, subType, null);
        }

        /// <summary>
        /// 将实体转为XML
        /// </summary>
        /// <typeparam name="T">RequestMessage或ResponseMessage</typeparam>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public static XDocument ConvertEntityToXml<T>(this T entity) where T : class, new()
        {
            //如果实体为空，生成一个新的对象
            entity = entity ?? new T();
            var doc = new XDocument();                                //生成一个新的XML文档对象
            doc.Add(new XElement("xml"));                           //增加根节点
            var root = doc.Root;                                            //得到根节点

            /* 注意！
             * 经过测试，微信对字段排序有严格要求，这里对排序进行强制约束
            */
            var propNameOrder = new List<string>() { "ToUserName", "FromUserName", "CreateTime", "MsgType" };
            //不同返回类型需要对应不同特殊格式的排序
            if (entity is ResponseMessageNews)
            {
                propNameOrder.AddRange(new[] { "ArticleCount", "Articles", "FuncFlag",/*以下是Atricle属性*/ "Title ", "Description ", "PicUrl", "Url" });
            }
            else if (entity is ResponseMessageTransfer_Customer_Service)
            {
                propNameOrder.AddRange(new[] { "TransInfo", "KfAccount", "FuncFlag" });
            }
            else if (entity is ResponseMessageMusic)
            {
                propNameOrder.AddRange(new[] { "Music", "FuncFlag", "ThumbMediaId",/*以下是Music属性*/ "Title ", "Description ", "MusicUrl", "HQMusicUrl" });
            }
            else if (entity is ResponseMessageImage)
            {
                propNameOrder.AddRange(new[] { "Image",/*以下是Image属性*/ "MediaId " });
            }
            else if (entity is ResponseMessageVoice)
            {
                propNameOrder.AddRange(new[] { "Voice",/*以下是Voice属性*/ "MediaId " });
            }
            else if (entity is ResponseMessageVideo)
            {
                propNameOrder.AddRange(new[] { "Video",/*以下是Video属性*/ "MediaId ", "Title", "Description" });
            }
            else
            {
                //如Text类型
                propNameOrder.AddRange(new[] { "Content", "FuncFlag" });
            }

            Func<string, int> orderByPropName = propNameOrder.IndexOf;                                                         //排序方法

            var props = entity.GetType().GetProperties().OrderBy(p => orderByPropName(p.Name)).ToList();       //按照propNameOrder的顺序对实体的属性进行排序
            foreach (var prop in props)
            {//迭代实体所有属性
                var propName = prop.Name;
                if (propName == "Articles")
                {
                    //文章列表
                    var atriclesElement = new XElement("Articles");                                                                             //生成临时XML元素对象
                    var articales = prop.GetValue(entity, null) as List<Article>;                                                           //取得实体的该属性值并转换类型  
                    foreach (var articale in articales)
                    {
                        var subNodes = ConvertEntityToXml(articale).Root.Elements();                                                //将泛型队列类型中的每一个元素转换为XML格式
                        atriclesElement.Add(new XElement("item", subNodes));                                                           //增加到临时XML元素对象
                    }
                    root.Add(atriclesElement);                                                                                                              //增加到XML文件中去
                }
                else if (propName == "TransInfo")
                {
                    var transInfoElement = new XElement("TransInfo");
                    var transInfo = prop.GetValue(entity, null) as List<CustomerServiceAccount>;
                    foreach (var account in transInfo)
                    {
                        var trans = ConvertEntityToXml(account).Root.Elements();
                        transInfoElement.Add(trans);
                    }

                    root.Add(transInfoElement);
                }
                else if (propName == "Music" || propName == "Image" || propName == "Video" || propName == "Voice")
                {
                    //音乐、图片、视频、语音格式
                    var musicElement = new XElement(propName);
                    var media = prop.GetValue(entity, null);// as Music;                                                                         //取得该属性值
                    var subNodes = ConvertEntityToXml(media).Root.Elements();                                                         //递归调用填充该复杂类下边的属性
                    musicElement.Add(subNodes);
                    root.Add(musicElement);
                }
                else if (propName == "PicList")
                {
                    var picListElement = new XElement("PicList");
                    var picItems = prop.GetValue(entity, null) as List<PicItem>;
                    foreach (var picItem in picItems)
                    {
                        var item = ConvertEntityToXml(picItem).Root.Elements();
                        picListElement.Add(item);
                    }
                    root.Add(picListElement);
                }
                //else if (propName == "KfAccount")
                //{
                //    //TODO:可以交给string处理
                //    root.Add(new XElement(propName, prop.GetValue(entity, null).ToString().ToLower()));
                //}
                else
                {
                    //其他非特殊类型
                    switch (prop.PropertyType.Name)
                    {
                        case "String":
                            root.Add(new XElement(propName,
                                             new XCData(prop.GetValue(entity, null) as string ?? "")));                                   //如果该属性类型为字符串类型，在XML文件中增加该节点
                            break;
                        case "DateTime":
                            root.Add(new XElement(propName,                                                                                    //该属性为日期时间类型，取得属性之后转换为微信时间类型后再XML文件中增加该节点
                                                  DateTimeHelper.GetWeixinDateTime(
                                                      (DateTime)prop.GetValue(entity, null))));
                            break;
                        case "Boolean":
                            if (propName == "FuncFlag")
                            {
                                root.Add(new XElement(propName, (bool)prop.GetValue(entity, null) ? "1" : "0"));      //对该类型下的“FuncFlag”属性转换为数字格式并增加到XML节点中
                            }
                            else
                            {                                                                                                                                           //其他“Boolean”类型的属性
                                goto default;
                            }
                            break;
                        case "ResponseMsgType":
                            root.Add(new XElement(propName, new XCData(prop.GetValue(entity, null).ToString().ToLower())));   //写入返回类型  将枚举变量转换为小写字符串加入XML中
                            break;
                        case "Article":                                                                                                                                                 //按当前代码可能不会执行到
                            root.Add(new XElement(propName, prop.GetValue(entity, null).ToString().ToLower()));
                            break;
                        case "TransInfo":
                            root.Add(new XElement(propName, prop.GetValue(entity, null).ToString().ToLower()));                        //按当前代码可能不会执行到
                            break;
                        default:
                            if (prop.PropertyType.IsClass && prop.PropertyType.IsPublic)
                            {//如果该属性为类或者委托类型并且该类型为公开类型
                                //自动处理其他实体属性
                                var subEntity = prop.GetValue(entity, null);                                                                 //生成一个临时实体对象
                                var subNodes = ConvertEntityToXml(subEntity).Root.Elements();                              //递归调用转换函数
                                root.Add(new XElement(propName, subNodes));                                                         
                            }
                            else
                            {
                                root.Add(new XElement(propName, prop.GetValue(entity, null)));                             //增加该属性到XML文件中
                            }
                            break;
                    }
                }
            }
            return doc;
        }

        /// <summary>
        /// 将实体转为XML字符串
        /// </summary>
        /// <typeparam name="T">RequestMessage或ResponseMessage</typeparam>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public static string ConvertEntityToXmlString<T>(this T entity) where T : class, new()
        {
            return entity.ConvertEntityToXml().ToString();
        }

        /// <summary>
        /// ResponseMessageBase.CreateFromRequestMessage&lt;T&gt;(requestMessage)的扩展方法
        /// </summary>
        /// <typeparam name="T">需要生成的ResponseMessage类型</typeparam>
        /// <param name="requestMessage">IRequestMessageBase接口下的接收信息类型</param>
        /// <returns></returns>
        public static T CreateResponseMessage<T>(this IRequestMessageBase requestMessage) where T : ResponseMessageBase
        {
            return ResponseMessageBase.CreateFromRequestMessage<T>(requestMessage);
        }

        /// <summary>
        /// ResponseMessageBase.CreateFromResponseXml(xml)的扩展方法
        /// </summary>
        /// <param name="xml">返回给服务器的Response Xml</param>
        /// <returns></returns>
        public static IResponseMessageBase CreateResponseMessage(this string xml)
        {
            return ResponseMessageBase.CreateFromResponseXml(xml);
        }

        /// <summary>
        /// 检查是否是通过场景二维码扫入
        /// </summary>
        /// <param name="requestMessage"></param>
        /// <returns></returns>
        public static bool IsFromScene(this RequestMessageEvent_Subscribe requestMessage)
        {
            return !string.IsNullOrEmpty(requestMessage.EventKey);
        }
    }
}
