using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

namespace System.StringFormats
{

    /// <summary>
    /// format: @[@][Type.]Member([arg0][,arg1]...)[,format]
    /// </summary>
    public class CallStringFormatProvider : IFormatProvider, ICustomFormatter
    {
        private ICustomFormatter baseFormatter;
        static Regex methodRegex = new Regex("\\s*(?<action>@{1,2})\\s*(?<name>[^\\s\\(,]+)\\s*(\\((?<param>(.*?)(?<!\\\\))\\))?\\s*(,(?<format>.*))?\\s*");
        static Regex paramRegex = new Regex("\\s*,?\\s*((\"(?<string>[^\"]*)\")|('(?<char>[^']*)')|(?<value>[^,\\s]+))\\s*");


        public CallStringFormatProvider()
        {

        }

        public CallStringFormatProvider(ICustomFormatter baseFormatter)
        {
            this.baseFormatter = baseFormatter;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            string result;
            if (HandleFormat(format, arg, out result))
                return result;

            if (baseFormatter != null)
                return baseFormatter.Format(format, arg, formatProvider);

            if (arg is IFormattable)
                return ((IFormattable)arg).ToString(format, CultureInfo.CurrentCulture);
            if (arg != null)
            {
                if (arg is string)
                    return (string)arg;
                else
                    return arg.ToString();
            }

            return string.Empty;
        }
        static ParameterInfo[] EmptyParameterInfoArray = new ParameterInfo[0];

        public bool HandleFormat(string format, object arg, out string result)
        {
            result = null;
            if (string.IsNullOrEmpty(format))
                return false;
            if (format[0] == '@')
            {
                string typeName = null;
                string methodName = null;
                string[] paramStrings = null;
                TypeCode[] paramTypeCodes;
                bool first = true;
                object instance = arg;
                string action;
                object ret = null;
                foreach (Match m in methodRegex.Matches(format))
                {
                    typeName = null;
                    methodName = null;
                    paramStrings = null;
                    paramTypeCodes = null;
                    methodName = m.Groups["name"].Value;
                    if (string.IsNullOrEmpty(methodName))
                        throw new Exception("method name null");
                    int dotIndex = methodName.LastIndexOf('.');
                    if (dotIndex >= 0)
                    {
                        typeName = methodName.Substring(0, dotIndex);
                        methodName = methodName.Substring(dotIndex + 1);
                    }

                    action = m.Groups["action"].Value;
                    string subFormat = m.Groups["format"].Value;
                    bool b = m.Groups["format"].Success;
                    string strParam = m.Groups["param"].Value;
                    if (!string.IsNullOrEmpty(strParam))
                    {
                        strParam = strParam.Replace("\\(", "(").Replace("\\)", ")");
                        var pms = paramRegex.Matches(strParam);
                        paramStrings = new string[pms.Count];
                        paramTypeCodes = new TypeCode[pms.Count];
                        for (int i = 0; i < pms.Count; i++)
                        {
                            Match pm = pms[i];
                            if (pm.Groups["value"].Success)
                            {
                                string value = pm.Groups["value"].Value;
                                paramStrings[i] = value;
                                paramTypeCodes[i] = TypeCode.Object;
                            }
                            else if (pm.Groups["string"].Success)
                            {
                                string str = pm.Groups["string"].Value;
                                paramStrings[i] = str;
                                paramTypeCodes[i] = TypeCode.String;
                            }
                            else if (pm.Groups["char"].Success)
                            {
                                string str = pm.Groups["char"].Value;
                                paramStrings[i] = str;
                                paramTypeCodes[i] = TypeCode.String;
                            }
                        }
                    }


                    Type memberType = null;

                    if (action == "@@")
                    {
                        memberType = instance as Type;
                        if (memberType == null)
                            throw new Exception("instance not System.Type type");
                    }
                    else if (!string.IsNullOrEmpty(typeName))
                    {
                        memberType = FindType(typeName);

                        if (memberType == null)
                            throw new Exception("not found type" + typeName);
                    }
                    else
                    {
                        if (instance == null)
                        {
                            if (methodName == "ToString")
                            {
                                result = string.Empty;
                                return true;
                            }
                            throw new Exception("Instance null, format: " + format);
                        }
                        if (instance is Type)
                            memberType = instance as Type;
                        else
                            memberType = instance.GetType();
                    }


                    object[] parameters = null;

                    MemberInfo member = null;
                    MethodInfo method = null;
                    int paramStringsLength = paramStrings == null ? 0 : paramStrings.Length;
                    ParameterInfo[] ps = null;
                    bool isParamArray = false;
                    BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Instance;

                    foreach (var mInfo in memberType.GetMembers(bindingFlags))
                    {
                        method = null;
                        if (mInfo.Name != methodName)
                            continue;
                        ps = EmptyParameterInfoArray;

                        if (mInfo is MethodInfo)
                        {
                            method = (MethodInfo)mInfo;
                            ps = method.GetParameters();
                        }
                        else if (mInfo is PropertyInfo)
                        {
                            method = ((PropertyInfo)mInfo).GetGetMethod(false);
                            ps = method.GetParameters();
                        }

                        if (ps.Length == 0)
                            isParamArray = false;
                        else
                            isParamArray = ps[ps.Length - 1].IsDefined(typeof(ParamArrayAttribute));

                        if (isParamArray)
                        {
                            if (paramStringsLength < ps.Length - 1)
                                continue;
                        }
                        else
                        {
                            if (ps.Length != paramStringsLength)
                                continue;
                        }

                        bool matchParamType = true;

                        for (int i = 0; i < (isParamArray ? ps.Length - 1 : ps.Length); i++)
                        {
                            if (paramTypeCodes[i] == TypeCode.String)
                            {
                                if (ps[i].ParameterType != typeof(string))
                                {
                                    matchParamType = false;
                                    break;
                                }
                            }
                            else if (paramTypeCodes[i] == TypeCode.Char)
                            {
                                if (ps[i].ParameterType != typeof(char))
                                {
                                    matchParamType = false;
                                    break;
                                }
                            }
                        }

                        if (!matchParamType)
                            continue;

                        member = mInfo;
                        break;
                    }

                    if (member == null)
                        throw new MissingMethodException(memberType.FullName, methodName);

                    if (ps.Length > 0)
                    {
                        parameters = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            var pInfo = ps[i];
                            if (isParamArray && i == ps.Length - 1)
                            {
                                Type elemType = pInfo.ParameterType.GetElementType();
                                Array array = Array.CreateInstance(elemType, paramStringsLength - i);
                                for (int j = 0; j < array.Length; j++)
                                {
                                    object val;
                                    if (paramStrings[i + j] == "$")
                                        val = instance;
                                    else
                                        val = paramStrings[i + j];
                                    val = Convert.ChangeType(val, elemType);
                                    array.SetValue(val, j);
                                }
                                parameters[i] = array;
                            }
                            else
                            {
                                object val;
                                if (paramStrings[i] == "$")
                                    val = instance;
                                else
                                    val = paramStrings[i];
                                val = Convert.ChangeType(val, pInfo.ParameterType);
                                parameters[i] = val;
                            }
                        }
                    }

                    if (method != null)
                    {
                        if (method.IsStatic)
                        {
                            ret = method.Invoke(null, parameters);
                        }
                        else
                        {
                            ret = method.Invoke(instance, parameters);
                        }
                    }
                    else
                    {
                        var fInfo = member as FieldInfo;
                        if (fInfo != null)
                        {
                            if (fInfo.IsStatic)
                                ret = fInfo.GetValue(null);
                            else
                                ret = fInfo.GetValue(instance);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    if (!string.IsNullOrEmpty(subFormat))
                    {
                        ret = string.Format("{0:" + subFormat + "}", ret);
                    }
                    instance = ret;
                }
                if (ret == null)
                    result = string.Empty;
                else if (ret is string)
                    result = (string)ret;
                else
                    result = ret.ToString();
                return true;
            }

            result = null;
            return false;
        }

        static Dictionary<string, Type> cachedTypes;
        static Type FindType(string typeName)
        {
            if (cachedTypes == null)
                cachedTypes = new Dictionary<string, Type>();
            Type type = null;
            if (!cachedTypes.TryGetValue(typeName, out type))
            {
                type = Type.GetType(typeName);
                if (type == null)
                {
                    int index = typeName.IndexOf('.');

                    type = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(o => o.GetTypes())
                        .Where(o =>
                        {
                            if (index < 0)
                            {
                                if (o.Name == typeName)
                                    return true;
                            }

                            if (o.FullName == typeName)
                                return true;
                            return false;
                        }).FirstOrDefault();
                }
                cachedTypes[typeName] = type;
            }
            return type;
        }


        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            return null;
        }
    }
}
