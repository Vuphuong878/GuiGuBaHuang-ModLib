﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ObjectHelper
{
    public static int goIndex = 0;

    public static readonly Newtonsoft.Json.JsonSerializerSettings CLONE_JSON_SETTINGS = new Newtonsoft.Json.JsonSerializerSettings
    {
        Formatting = Newtonsoft.Json.Formatting.Indented,
        TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
        PreserveReferencesHandling = Newtonsoft.Json.PreserveReferencesHandling.Objects,
    };

    public static T Clone<T>(this T obj)
    {
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(obj, CLONE_JSON_SETTINGS), CLONE_JSON_SETTINGS);
    }

    public static void Map<T>(T src, T dest)
    {
        Map(src, dest, typeof(T));
    }

    public static void Map(object src, object dest, System.Type objType)
    {
        foreach (var p in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanWrite || !p.CanRead)
                continue;
            var srcValue = p.GetValue(src);
            if (srcValue != null)
                p.SetValue(dest, srcValue);
        }
    }

    public static void MapBySourceProp(object src, object dest)
    {
        foreach (var p1 in src.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var p2 = dest.GetType().GetProperty(p1.Name, BindingFlags.Public | BindingFlags.Instance);
            if (p2 == null || !p2.CanWrite || !p1.CanRead)
                continue;
            var srcValue = p1.GetValue(src);
            if (srcValue != null)
                p2.SetValue(dest, srcValue);
        }
    }

    public static void MapByDestProp(object src, object dest)
    {
        foreach (var p1 in dest.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var p2 = src.GetType().GetProperty(p1.Name, BindingFlags.Public | BindingFlags.Instance);
            if (p2 == null || !p1.CanWrite || !p2.CanRead)
                continue;
            var srcValue = p2.GetValue(src);
            if (srcValue != null)
                p1.SetValue(dest, srcValue);
        }
    }

    public static FieldInfo GetField(this object obj, string fieldNm)
    {
        return obj.GetType().GetField(fieldNm);
    }

    public static PropertyInfo GetProperty(this object obj, string fieldNm)
    {
        return obj.GetType().GetProperty(fieldNm);
    }

    public static object GetValueUnsafe(this object obj, string fieldNm)
    {
        return obj.GetType().GetProperty(fieldNm).GetValue(obj);
    }

    public static object GetValue(this object obj, string fieldNm, bool ignorePropertyNotFoundError = false)
    {
        var prop = obj.GetType().GetProperty(fieldNm);
        if (prop == null)
        {
            if (ignorePropertyNotFoundError)
                return null;
            throw new NullReferenceException();
        }
        return prop.GetValue(obj);
    }

    public static void SetValueUnsafe(this object obj, string fieldNm, object newValue)
    {
        obj.GetType().GetProperty(fieldNm).SetValue(obj, newValue);
    }

    public static void SetValue(this object obj, string fieldNm, object newValue, bool ignorePropertyNotFoundError = false, Func<Type, object> customParser = null)
    {
        var prop = obj.GetType().GetProperty(fieldNm);
        if (prop == null)
        {
            if (ignorePropertyNotFoundError)
                return;
            throw new NullReferenceException();
        }
        var type = prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ? Nullable.GetUnderlyingType(prop.PropertyType) : prop.PropertyType;
        if (ParseHelper.TryParseUnknown(newValue, type, out object parsedValue))
        {
            prop.SetValue(obj, parsedValue);
        }
        else
        {
            if (customParser == null)
            {
                //ignore
            }
            else
            {
                prop.SetValue(obj, customParser(type));
            }
        }
    }

    public static bool IsDeclaredMethod(this object obj, string medName)
    {
        return obj?.GetType()?.GetMethod(medName)?.DeclaringType == obj.GetType();
    }

    public static string GetBackingFieldName(string propertyName)
    {
        return string.Format("<{0}>k__BackingField", propertyName);
    }

    public static FieldInfo GetBackingField(object obj, string propertyName)
    {
        return obj.GetType().GetField(GetBackingFieldName(propertyName), BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static void SetBackingField(object obj, string propertyName, object value)
    {
        GetBackingField(obj, propertyName).SetValue(obj, value);
    }
}