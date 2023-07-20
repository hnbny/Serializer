using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.Json;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Program
{
    static class JsonSerializer
    {

        static public string Serialize(object obj)
        {
            if (obj == null)
            {
                return "null";
            }

            string str = "";
            Type type = obj.GetType();

            // Для string
            if (type == typeof(string))
            {
                str += "\"";
                foreach (char c in (string)obj)
                {
                    switch (c)
                    {
                        case '/': // \/
                            str += "\\/";
                            break;

                        case '\\':
                            str += "\\\\";
                            break;

                        case '\"':
                            str += String.Format(@"\u{0:x4}", (ushort)c);
                            break;

                        case '\b':
                            str += "\\b";
                            break;
                        case '\f':
                            str += "\\f";
                            break;
                        case '\n':
                            str += "\\n";
                            break;
                        case '\r':
                            str += "\\r";
                            break;
                        case '\t':
                            str += "\\t";
                            break;

                        default:
                            if (c < 32 || c >= 127 && c <= 160 || c == 173) //https://www.aivosto.com/articles/control-characters.html#list_C0
                            {
                                str += String.Format(@"\u{0:x4}", (ushort)c);
                            }
                            else
                            {
                                str += c;
                            }
                            break;
                    }
                }
                str += "\"";
                return str;
            }

            // Для int
            else if (type == typeof(int))
            {
                str = obj.ToString();
                return str;
            }

            // Для double
            else if (type == typeof(double))
            {
                double d = (double)obj;
                str = d.ToString("E");
                return str;
            }

            // Для bool
            else if (type == typeof(bool))
            {
                str = (bool)obj == true ? "true" : "false";
            }



            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                List<object> list = new List<object>();
                foreach (var item in (IEnumerable)obj)
                {
                    list.Add(item);
                }

                str += "[ ";
                foreach (var item in list)
                {
                    str += (Serialize(item) + ",");
                }
                str = str[..^1];
                str += " ]";
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                Type[] genericArguments = type.GetGenericArguments();
                Type t1 = genericArguments[1];
                foreach (var item in ((IEnumerable)obj))
                {
                    var key = item.GetType().GetProperty("Key").GetValue(item).ToString();
                    var value = item.GetType().GetProperty("Value").GetValue(item);
                    d.Add(key, value);
                }
                str += "{ ";
                foreach (var item in d)
                {
                    str += ("\"" + item.Key + "\"" + ":");
                    str += (Serialize(item.Value) + ",");
                }
                str = str[..^1];
                str += " }";
            }


            // Для class и struct
            else if (type.IsClass || type.IsValueType && !type.IsEnum)
            {
                str += "{ class:" + type.Name + " ";
                FieldInfo[] info = type.GetFields();
                foreach (FieldInfo field in info)
                {
                    str += field.Name + ":";
                    str += Serialize(field.GetValue(obj)) + " ";
                }
                str += "}";
            }

            return str;
        }


        static public object Deserialize(string s)
        {
            // Для string
            if (s[0] == '\"')
            {
                string str = "";
                for (int i = 1; i < s.Length - 1;)
                {
                    if (s[i] == '\\') // Значит это управляющий символ
                    {
                        i++;
                        switch (s[i])
                        {
                            case '/':
                                str += "/"; i++;
                                break;

                            case '\\':
                                str += "\\"; i++;
                                break;

                            case '\'':
                                str += "\'"; i++;
                                break;

                            case '\"':
                                str += "\""; i++;
                                break;

                            case '\b':
                                str += "\b"; i++;
                                break;

                            case '\f':
                                str += "\f"; i++;
                                break;

                            case '\n':
                                str += "\n"; i++;
                                break;

                            case '\r':
                                str += "\r"; i++;
                                break;

                            case '\t':
                                str += "\t"; i++;
                                break;

                            case 'u':
                                i++;
                                int decValue = Convert.ToInt32(s.Substring(i, 4), 16);
                                str += (char)decValue;
                                i += 4;
                                break;

                            default:

                                str += s[i];
                                break;
                        }
                    }

                    else
                    {
                        str += s[i];
                        i++;
                    }
                }
                return str;
            }

            // Для int и double
            else if (Char.IsDigit(s[0]) || s[0] == '-')
            {
                int num;
                bool isInt = int.TryParse(s, out num);

                return isInt ? num : double.Parse(s);
            }

            // Для bool
            else if (s == "true" || s == "false" || s == "null")
            {
                return s == "true" ? true : (s == "false" ? false : null);
            }

            // Для Dictionary, class и struct
            else if (s[0] == '{')
            {
                // class и struct
                // Я НЕ ЗНАЮ, ПОЧЕМУ ОНО НЕ РАБОТАЕТ
                if (s.Substring(2, 5) == "class")
                {
                    int i = 8;

                    string nameOfClass = String.Empty;
                    for (; ; i++)
                    {
                        if (s[i] != ' ')
                        {
                            nameOfClass += s[i];
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }


                    Type t = Type.GetType("Program." + nameOfClass); // Имя класса

                    ConstructorInfo[] constructors = t.GetConstructors(); // Получаем информацию о всех конструкторах класса


                    // Будем считать, что порядок переменных в классе совпадает с порядком переменных в сигнатуре конструктора
                    // И конструктор в классе один


                    object[] values = new object[0]; // Значения переменных, принимаемые конструктором
                    string str = string.Empty;
                    for (; i < s.Length - 2; i++) // Тут получаем значения, хранящиеся в объекте
                    {
                        if (s[i] == ':')
                        {
                            str = String.Empty;
                        }
                        else if (s[i] == ' ')
                        {
                            values = values.Append(Deserialize(str)).ToArray();
                            str = String.Empty;
                        }
                        else
                        {
                            str += s[i];
                        }
                    }
                    if (str != String.Empty)
                    {
                        values = values.Append(Deserialize(str)).ToArray();
                    }

                    ParameterInfo[] parameters = constructors[0].GetParameters();
                    for (int l = 0; l < values.Length; l++)
                    {
                        // Тут нужно для всех элементов массива values поменять типы данных на те, что находятся в массиве parameters
                    }
                    object obj = constructors[0].Invoke(values); // Создаем объект


                    return obj;
                }

                // Dictionary
                else
                {
                    Stack<char> stack = new Stack<char>();
                    var dict = new Dictionary<object, object>();
                    string str = "";

                    for (int i = 2; i <= s.Length - 3; i++)
                    {
                        if (s[i] == '[')
                        {
                            stack.Push('[');
                            str += s[i];
                        }
                        else if (s[i] == '{')
                        {
                            stack.Push('{');
                            str += s[i];
                        }

                        else if (s[i] == ']')
                        {
                            str += s[i];

                            if (stack.Peek() == '[')
                            {
                                stack.Pop();
                            }
                        }
                        else if (s[i] == '}')
                        {
                            str += s[i];

                            if (stack.Peek() == '{')
                            {
                                stack.Pop();
                            }
                        }

                        else if (s[i] == ',')
                        {
                            if (stack.Count == 0)
                            {
                                string key, value;
                                for (int j = 0; j < str.Length; j++)
                                {
                                    if (str.Substring(j, 3) == "\":\"")
                                    {
                                        key = str.Substring(0, j + 1);
                                        value = str.Substring(j + 2, str.Length - j - 2);
                                        dict.Add(Deserialize(key), Deserialize(value));
                                        break;
                                    }
                                }

                                str = String.Empty;
                            }
                            else
                            {
                                str += ',';
                            }
                        }

                        else
                            str += s[i];
                    }

                    if (str != String.Empty)
                    {
                        string key, value;
                        for (int j = 0; j < str.Length; j++)
                        {
                            if (str.Substring(j, 3) == "\":\"")
                            {
                                key = str.Substring(0, j + 1);
                                value = str.Substring(j + 2, str.Length - j - 2);
                                dict.Add(Deserialize(key), Deserialize(value));
                                break;
                            }
                        }
                    }

                    return dict;
                }
            }

            // Для List
            else if (s[0] == '[')
            {
                Stack<char> stack = new Stack<char>();
                var list = new List<object>();
                string str = String.Empty;

                for (int i = 2; i <= s.Length - 3; i++)
                {
                    if (s[i] == '[')
                    {
                        stack.Push('[');
                        str += s[i];
                    }
                    else if (s[i] == '{')
                    {
                        stack.Push('{');
                        str += s[i];
                    }

                    else if (s[i] == ']')
                    {
                        str += s[i];

                        if (stack.Peek() == '[')
                        {
                            stack.Pop();
                        }
                    }
                    else if (s[i] == '}')
                    {
                        str += s[i];

                        if (stack.Peek() == '{')
                        {
                            stack.Pop();
                        }
                    }

                    else if (s[i] == ',')
                    {
                        if (stack.Count == 0)
                        {
                            list.Add(Deserialize(str));
                            str = String.Empty;
                        }
                        else
                        {
                            str += ',';
                        }
                    }

                    else
                        str += s[i];
                }

                if (str != String.Empty)
                    list.Add(Deserialize(str));

                return list;
            }

            else
            {
                throw new Exception("Deserealization was a failure");
            }
        }
    }



// test
/*    class A
    {
        public A(int a, double d, string s)
        {
            this.a = a;
            this.d = d;
            this.s = s;

        }
        public int a;
        public double d = 3.4;
        public string s;
    }*/


    public class Program
    {
        public static void Main()
        {
            
        }
    }
}
