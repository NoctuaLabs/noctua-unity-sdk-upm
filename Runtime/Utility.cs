using System;
using System.Collections.Generic;
using System.Reflection;

namespace com.noctuagames.sdk
{
    public static class Utility
    {
        public static string PrintFields<T>(this T obj)
        {
            var sb = new System.Text.StringBuilder();
            PrintFieldsRecursive(obj, 0, sb);

            return sb.ToString();
        }

        private static void PrintFieldsRecursive(object obj, int indentLevel, System.Text.StringBuilder sb)
        {
            if (obj == null)
            {
                return;
            }

            var type = obj.GetType();
            
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                return;
            }
            
            if (type.IsArray && obj is Array array)
            {
                sb.AppendLine(new string(' ', indentLevel * 2) + $"{type.Name}:");

                foreach (var item in array)
                {
                    PrintFieldsRecursive(item, indentLevel + 1, sb);
                }

                return;
            }
            
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (obj is not System.Collections.IList list) return;
                
                sb.AppendLine(new string(' ', indentLevel * 2) + $"{type.Name}:");

                foreach (var item in list)
                {
                    PrintFieldsRecursive(item, indentLevel + 1, sb);
                }

                return;
            }
            
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (obj is not System.Collections.IDictionary dictionary) return;
            
                foreach (var key in dictionary.Keys)
                {
                    var value = dictionary[key];
                    sb.AppendLine(new string(' ', indentLevel * 2) + $"{key}: {value}");
            
                    PrintFieldsRecursive(value, indentLevel + 1, sb);
                }
            
                return;
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                sb.AppendLine(new string(' ', indentLevel * 2) + $"{field.Name}: {value}");

                PrintFieldsRecursive(value, indentLevel + 1, sb);
            }
        }
    }
}