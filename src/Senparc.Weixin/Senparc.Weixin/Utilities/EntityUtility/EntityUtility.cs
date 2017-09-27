#region Apache License Version 2.0
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

    文件名：EntityUtility.cs
    文件功能描述：实体工具类


    创建标识：Senparc - 20160808（v4.6.0）

----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Globalization;                    //全球化
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Senparc.Weixin.Entities;
using Senparc.Weixin.Helpers;

namespace Senparc.Weixin.EntityUtility
{
    /// <summary>
    /// 实体工具类
    /// </summary>
    public static class EntityUtility
    {
        /// <summary>
        /// 将对象转换到指定类型   2017-9-27日以服务号的方式进行测试，不存在空值情况，也不存在泛型的情况
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="convertibleValue"></param>
        /// <returns></returns>
        public static T ConvertTo<T>(this IConvertible convertibleValue)
        {
            if (null == convertibleValue)
            {//如果需要转换的变量为空，生成指定类型的默认值
                return default(T);
            }
            if (!typeof(T).IsGenericType)
            {//查看当前需要转换的类型是否为泛型类型
                return (T)Convert.ChangeType(convertibleValue, typeof(T));    //非泛型类型,将变量转换为指定类型
            }
            else
            {//泛型类型
                Type genericTypeDefinition = typeof(T).GetGenericTypeDefinition();     //得到可用于构造当前泛型类型的泛型类型定义的 Type 对象
                if (genericTypeDefinition == typeof(Nullable<>))
                {//泛型类型为可空类型         开放类型是typeof(System.Nullable<>)
                    return (T)Convert.ChangeType(convertibleValue, Nullable.GetUnderlyingType(typeof(T))); //将变量转换为指定类型,该类型为可空类型的基础类型  如int？的基础类型为int
                }
            }
            throw new InvalidCastException(string.Format("Invalid cast from type \"{0}\" to type \"{1}\".", convertibleValue.GetType().FullName, typeof(T).FullName));
        }


        /// <summary>
        /// 向属性填充值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">实体对象</param>
        /// <param name="prop">将填充的属性</param>
        /// <param name="value">属性值--由调用函数传递过来的是由XML文件读取的字符串类型值</param>
        public static void FillSystemType<T>(T entity, PropertyInfo prop, IConvertible value)
        {
           
            FillSystemType(entity, prop, value, prop.PropertyType);
        }

        /// <summary>
        /// 向属性填充值（强制使用指定的类型）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">实体对象</param>
        /// <param name="prop">将填充的属性</param>
        /// <param name="value">属性值--由调用函数传递过来的是由XML文件读取的字符串类型值</param>
        /// <param name="specialType">属性类型</param>
        public static void FillSystemType<T>(T entity, PropertyInfo prop, IConvertible value, Type specialType)
        {
           
            object setValue = null;

            if (value.GetType() != specialType)
            {//数据类型不相同 
                switch (specialType.Name)
                {//根据不同的数据类型，将value（XML文件中的字符串）转换为不同的数据
                    case "Boolean":
                        setValue = value.ConvertTo<bool>();
                        break;
                    case "DateTime":
                        //在微信服务器转发过来的XML中的CreateTime节点，表示1970年1月1日0时0分0秒至消息创建时所间隔的秒数   
                        //将其转换为C#的DateTime类型
                        setValue = DateTimeHelper.GetDateTimeFromXml(value.ToString());
                        break;
                    case "Int32":
                        setValue = value.ConvertTo<int>();
                        break;
                    case "Int64":
                        setValue = value.ConvertTo<long>();
                        break;
                    case "Double":
                        setValue = value.ConvertTo<double>();
                        break;
                    case "String":
                        //CultureInfo 类保存区域性特定的信息,如关联的语言、子语言、国家/地区、日历和区域性约定
                        //CultureInfo.InvariantCulture 属性既不是非特定区域性，也不是特定区域性。它是第三种类型的区域性，该区域性是不区分区域性的。它与英语语言关联，但不与任何国家 / 地区关联
                        //如果要执行不受 CultureInfo.CurrentCulture 的值影响的区分区域性的字符串操作，则使用接受 CultureInfo 参数的方法，为该 CultureInfo 参数指定 CultureInfo.InvariantCulture 属性的值
                        setValue = value.ToString(CultureInfo.InvariantCulture);
                        break;
                    default:
                        setValue = value;
                        break;
                }
            }
           
            switch (specialType.Name)
            {
                case "Nullable`1": //可为空对象
                    {
                       
                        if (!string.IsNullOrEmpty(value as string))
                        {//判断字符串是空引用，或值为空  即 如果存在属性值
                            var genericArguments = prop.PropertyType.GetGenericArguments();   //取得 泛型类型的类型参数或泛型类型定义的类型参数的 Type 对象的数组
                            FillSystemType(entity, prop, value, genericArguments[0]);                     //递归设置属性值，取得第一个泛型参数类型作为将要设置的数据类型
                        }
                        else
                        {//文化素质相应属性值，属性值为空
                            prop.SetValue(entity, null, null);//默认通常为null
                        }
                        break;
                    }
                //case "String":
                //    goto default;
                //case "Boolean":
                //case "DateTime":
                //case "Int32":
                //case "Int64":
                //case "Double":
                default:
                    //设置实体的相应属性值  -- 如果没有进行类型转换，直接使用原始值
                    prop.SetValue(entity, setValue ?? value, null);
                    break;
            }
        }

        ///// <summary>
        ///// 将ApiData专为Dictionary类型
        ///// </summary>
        ///// <param name="apiData"></param>
        //public static Dictionary<string, string> ConvertDataEntityToDictionary<T>(T apiData)
        //    where T : IApiData
        //{
        //    Dictionary<string, string> dic = new Dictionary<string, string>();
        //    var props = typeof(T).GetProperties(BindingFlags.Public);
        //    foreach (var propertyInfo in props)
        //    {
        //        dic[propertyInfo.Name] = (propertyInfo.GetValue(apiData) ?? "").ToString();
        //    }
        //    return dic;
        //}
    }
}
