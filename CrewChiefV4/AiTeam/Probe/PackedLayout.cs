using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Berechnet Byte-Offsets in Strukturen mit LayoutKind.Sequential und Pack = 1 – auch durch
    /// generische Felder (Vector3&lt;T&gt;, TireData&lt;T&gt;), für die Marshal.OffsetOf nicht nutzbar ist.
    /// Pfade: "Player.Position.X", "TireTemp.FrontLeft.CurrentTemp.Center", "DriverData[3].Place".
    /// Verify prüft die eigene Rechnung gegen Marshal.SizeOf/OffsetOf der Wurzelstruktur.
    /// </summary>
    internal static class PackedLayout
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<Type, int> sizes = new Dictionary<Type, int>();
        private static readonly Dictionary<string, KeyValuePair<int, Type>> paths = new Dictionary<string, KeyValuePair<int, Type>>();

        internal static FieldInfo[] Fields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // Reihenfolge der Deklaration = Reihenfolge im Speicher (LayoutKind.Sequential).
            Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return fields;
        }

        internal static int Size(Type type)
        {
            lock (gate)
            {
                int size;
                if (sizes.TryGetValue(type, out size)) return size;
                size = ComputeSize(type);
                sizes[type] = size;
                return size;
            }
        }

        private static int ComputeSize(Type type)
        {
            if (type == typeof(byte) || type == typeof(sbyte)) return 1;
            if (type == typeof(short) || type == typeof(ushort)) return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;
            if (type.IsPrimitive || type.IsEnum)
                throw new NotSupportedException("Feldtyp ohne festes Speicherformat: " + type.Name);
            if (!type.IsValueType || type.IsArray)
                throw new NotSupportedException("Nur Strukturen mit festem Layout: " + type.Name);
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            if (layout == null || layout.Value != LayoutKind.Sequential || layout.Pack != 1)
                throw new NotSupportedException("Struktur ist nicht Sequential/Pack=1: " + type.Name);
            int total = 0;
            foreach (FieldInfo field in Fields(type)) total += FieldSize(field);
            return total;
        }

        private static int FieldSize(FieldInfo field)
        {
            if (!field.FieldType.IsArray) return Size(field.FieldType);
            return ArrayLength(field) * Size(field.FieldType.GetElementType());
        }

        private static int ArrayLength(FieldInfo field)
        {
            var marshal = Attribute.GetCustomAttribute(field, typeof(MarshalAsAttribute)) as MarshalAsAttribute;
            if (marshal == null || marshal.Value != UnmanagedType.ByValArray || marshal.SizeConst <= 0)
                throw new NotSupportedException("Feld ohne feste Länge: " + field.Name);
            return marshal.SizeConst;
        }

        /// <summary>Offset in Bytes ab Beginn von <paramref name="root"/>.</summary>
        internal static int Offset(Type root, string path)
        {
            Type ignored;
            return Offset(root, path, out ignored);
        }

        internal static int Offset(Type root, string path, out Type leaf)
        {
            string key = root.FullName + "|" + path;
            lock (gate)
            {
                KeyValuePair<int, Type> cached;
                if (paths.TryGetValue(key, out cached)) { leaf = cached.Value; return cached.Key; }
            }
            int offset = 0;
            Type current = root;
            foreach (string rawSegment in path.Split('.'))
            {
                string name = rawSegment;
                int index = -1;
                int open = rawSegment.IndexOf('[');
                if (open >= 0)
                {
                    int close = rawSegment.IndexOf(']', open);
                    if (close < 0) throw new ArgumentException("Ungültiger Pfad: " + path);
                    name = rawSegment.Substring(0, open);
                    index = int.Parse(rawSegment.Substring(open + 1, close - open - 1), System.Globalization.CultureInfo.InvariantCulture);
                }
                FieldInfo target = null;
                foreach (FieldInfo field in Fields(current))
                {
                    if (field.Name == name) { target = field; break; }
                    offset += FieldSize(field);
                }
                if (target == null) throw new ArgumentException("Feld nicht gefunden: " + name + " in " + current.Name);
                if (index >= 0)
                {
                    if (!target.FieldType.IsArray) throw new ArgumentException("Kein Array: " + name);
                    int length = ArrayLength(target);
                    if (index >= length) throw new ArgumentOutOfRangeException(path);
                    current = target.FieldType.GetElementType();
                    offset += index * Size(current);
                }
                else
                {
                    current = target.FieldType;
                }
            }
            leaf = current;
            lock (gate) paths[key] = new KeyValuePair<int, Type>(offset, leaf);
            return offset;
        }

        /// <summary>Null, wenn die eigene Rechnung mit dem Marshaller übereinstimmt; sonst eine Beschreibung.</summary>
        internal static string Verify(Type root)
        {
            try
            {
                int own = Size(root), marshal = Marshal.SizeOf(root);
                if (own != marshal) return "Größe " + own + " statt " + marshal + " Byte";
                foreach (FieldInfo field in Fields(root))
                {
                    int expected = Marshal.OffsetOf(root, field.Name).ToInt32();
                    int actual = Offset(root, field.Name);
                    if (expected != actual) return "Offset " + field.Name + ": " + actual + " statt " + expected;
                }
                return null;
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }
    }
}
