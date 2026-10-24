using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;

namespace CrewChiefV4
{
    public static class JsonFiles
    {
        /// <summary>
        /// Whether to strip lines starting with # from the file.
        /// </summary>
        public enum Comments
        {
            ThereAreNone,
            Strip
        }

        public static T ReadFile<T>(string filePath, Comments stripComments = Comments.ThereAreNone, bool dontCatch = false)
        {
            T result = default(T);
            string json = readFile(filePath, stripComments);
            try
            {
                result = JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonReaderException ex)
            {
                if (dontCatch)
                {
                    throw ex;
                }
                Log.Error($"Error reading JSON file '{filePath}' : {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Read the JSON file, calling the onException callback if an exception occurs.
        /// </summary>
        /// <param name="onException"></param>
        /// <returns></returns>
        public static T TryReadFile<T>(this FileInfo file, Action<Exception> onException = null) where T : class
        {
            try
            {
                return ReadFile<T>(file.FullName, Comments.ThereAreNone, true);
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
                return default(T);
            }
        }

        /// <param name="filterNonValues">null values, empty strings, -1 numeric 
        /// values and empty collections will be omitted from the output.
        /// Ffloat .0 values will be written as integers</param>
        /// <returns>true if writing succeeded</returns>
        public static bool WriteFile(string filePath, object data, bool filterNonValues = false)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            return writeFile(filePath, data, serializer, filterNonValues);
        }

        /// <summary>
        /// null values, empty strings, -1 numeric values and 
        /// empty collections will be omitted from the output.
        /// Float .0 values will be written as integers
        /// </summary>
        /// <returns>true if writing succeeded</returns>
        public static bool WriteFilteredFile(string filePath, object data)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.DefaultValueHandling = DefaultValueHandling.Ignore;
            serializer.ContractResolver = new IgnoreEmptyValuesResolver();
            serializer.Converters.Add(new FloatWithoutTrailingZeroConverter());
            return writeFile(filePath, data, serializer);
        }
        private static bool writeFile(string filePath, object data, JsonSerializer serializer, bool filterNonValues = false)
        { 
            bool result = false;
            if (filePath != null)
            {
                try
                {
                    using (StreamWriter file = File.CreateText(filePath))
                    {
                        serializer.Formatting = Formatting.Indented;
                        if (filterNonValues)
                        {
                            serializer.NullValueHandling = NullValueHandling.Ignore;
                            serializer.DefaultValueHandling = DefaultValueHandling.Ignore;
                            serializer.ContractResolver = new IgnoreEmptyValuesResolver();
                            serializer.Converters.Add(new FloatWithoutTrailingZeroConverter());
                        }
                        serializer.Serialize(file, data);
                        result = true;
                    }
                }
                catch (Exception e)
                {
                    // use C.W instead of Log.Error as writing to log file may not be set up yet
                    Console.WriteLine($"Error writing JSON file '{Path.GetFullPath(filePath)}' : {e.Message}");
                }
            }
            return result;
        }

        public class FloatWithoutTrailingZeroConverter : JsonConverter<float>
        {
            public override void WriteJson(JsonWriter writer, float value, JsonSerializer serializer)
            {
                if (value % 1 == 0)
                {
                    writer.WriteRawValue(((long)value).ToString());
                }
                else
                {
                    writer.WriteRawValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            public override float ReadJson(
                JsonReader reader,
                Type objectType,
                float existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                return Convert.ToSingle(reader.Value);
            }
        }

        public class IgnoreEmptyValuesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(
                MemberInfo member,
                MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);

                Predicate<object> baseShouldSerialize = property.ShouldSerialize;

                property.ShouldSerialize = obj =>
                {
                    if (baseShouldSerialize != null &&
                        !baseShouldSerialize(obj))
                    {
                        return false;
                    }

                    object value = property.ValueProvider.GetValue(obj);

                    // null
                    if (value == null)
                    {
                        return false;
                    }

                    // string
                    string str = value as string;
                    if (str != null)
                    {
                        return !string.IsNullOrWhiteSpace(str);
                    }

                    // numeric -1
                    if (IsNegativeOne(value))
                    {
                        return false;
                    }

                    // collections
                    if (!(value is string) && value is IEnumerable)
                    {
                        IEnumerable enumerable = (IEnumerable)value;

                        bool hasValidValue = false;

                        foreach (object item in enumerable)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            string itemString = item as string;

                            // skip empty strings
                            if (itemString != null)
                            {
                                if (!string.IsNullOrWhiteSpace(itemString))
                                {
                                    hasValidValue = true;
                                    break;
                                }

                                continue;
                            }

                            // skip -1 values
                            if (IsNegativeOne(item))
                            {
                                continue;
                            }

                            hasValidValue = true;
                            break;
                        }

                        return hasValidValue;
                    }

                    return true;
                };

                return property;
            }

            private static bool IsNegativeOne(object value)
            {
                if (value is sbyte) return (sbyte)value == -1;
                if (value is short) return (short)value == -1;
                if (value is int) return (int)value == -1;
                if (value is long) return (long)value == -1;
                if (value is float) return (float)value == -1;
                if (value is double) return (double)value == -1;
                if (value is decimal) return (decimal)value == -1;
                return false;
            }
        }

        private static string readFile(string filePath, Comments stripComments = Comments.ThereAreNone)
        {
            if (filePath == null)
            {
                Log.Error("File path is null");
                return null;
            }
            if (!File.Exists(filePath))
            {
                // use C.W instead of Log.Error as writing to log file may not be set up yet
                Console.WriteLine($"File '{Path.GetFullPath(filePath)}' does not exist");
                return null;
            }
            try
            {
                using (StreamReader file = File.OpenText(filePath))
                {
                    StringBuilder sb = new StringBuilder();
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (stripComments == Comments.Strip && line.Trim().StartsWith("#"))
                        {
                            continue;
                        }
                        sb.AppendLine(line);
                    }
                    return sb.ToString();
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error reading text file " + filePath + ": ");
                return null;
            }
        }
    }
}