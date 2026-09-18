using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Deliberately excludes Unity instance IDs: they are not stable across game launches.
public static class SavedScalarFields
{
    [Serializable] public sealed class Value
    {
        public string declaringType, name, text;
        public float number;
        public int integer;
        public bool boolean;
        public Color color;
        public Vector3 vector;
    }

    public static List<Value> Capture(object target, bool runtimeFields = false)
    {
        var values = new List<Value>();
        for (var type = target.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.IsInitOnly || (!runtimeFields && !field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField)))) continue;
            var t = field.FieldType;
            if (!(t == typeof(string) || t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(Color) || t == typeof(Vector2) || t == typeof(Vector3) || t.IsEnum)) continue;
            var v = new Value { declaringType = type.FullName, name = field.Name };
            var raw = field.GetValue(target);
            if (t == typeof(string)) v.text = (string)raw;
            else if (t == typeof(float)) v.number = (float)raw;
            else if (t == typeof(int) || t.IsEnum) v.integer = Convert.ToInt32(raw);
            else if (t == typeof(bool)) v.boolean = (bool)raw;
            else if (t == typeof(Color)) v.color = (Color)raw;
            else if (t == typeof(Vector2)) v.vector = (Vector2)raw;
            else v.vector = (Vector3)raw;
            values.Add(v);
        }
        return values;
    }

    public static void Apply(object target, List<Value> values)
    {
        if (values == null) return;
        foreach (var v in values)
        {
        // Shared character fields moved up one level without changing their YAML names.
        // Older disk snapshots also store the declaring CLR type, so map that explicitly.
        string declaringType = v.declaringType;
        if (target is ZeldaCharacterCommonData && declaringType == typeof(ZeldaCharacterData).FullName &&
            typeof(ZeldaCharacterCommonData).GetField(v.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null)
            declaringType = typeof(ZeldaCharacterCommonData).FullName;
        for (var type = target.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            if (type.FullName != declaringType) continue;
            var f = type.GetField(v.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null || f.IsInitOnly) break;
            var t = f.FieldType;
            object raw = t == typeof(string) ? (object)v.text : t == typeof(float) ? v.number :
                t == typeof(int) ? v.integer : t == typeof(bool) ? v.boolean :
                t == typeof(Color) ? v.color : t == typeof(Vector2) ? (object)(Vector2)v.vector :
                t == typeof(Vector3) ? v.vector : t.IsEnum ? Enum.ToObject(t, v.integer) : null;
            if (raw != null) f.SetValue(target, raw);
            break;
        }
        }
    }
}
