using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace Cottontail.Data
{
    /// <summary>
    /// Type conversion utilities and extensions
    /// </summary>
    public static class TypeUtility
    {

        //Enum stuff, allows for the use of the descriptuions attribute for descriptive string value conversion
        public static string ToDescription<TEnum>(this TEnum EnumValue) where TEnum : struct
        {
            return GetEnumDescription((Enum)(object)(EnumValue));
        }

        public static string GetEnumDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(
                typeof(DescriptionAttribute),
                false);

            if (attributes != null &&
                attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }

        #region "Raw Type Conversion"
        /// <summary>
        /// Fault safe type conversion with output reference and verification (like value type TryConvert)
        /// </summary>
        /// <typeparam name="T">the type to convert to</typeparam>
        /// <param name="thing">the thing being converted</param>
        /// <param name="newThing">the converted thing as output</param>
        /// <param name="def">The default value, which defaults to default(type)</param>
        /// <returns>success status</returns>
        public static bool TryConvert<T>(object thing, ref T newThing, T def = default(T))
        {
            try
            {
                if (thing == null)
                    return false;

                if (typeof(T).IsEnum)
                {
                    if (thing is short || thing is int)
                        newThing = (T)thing;
                    else
                        newThing = (T)Enum.Parse(typeof(T), thing.ToString());
                }
                else
                    newThing = (T)Convert.ChangeType(thing, typeof(T));

                return true;
            }
            catch
            {
                //dont error on tryconvert, it's called TRY for a reason
                newThing = def;
            }

            return false;
        }

        /// <summary>
        /// Fault safe type conversion with fluent return
        /// </summary>
        /// <typeparam name="T">the type to convert to</typeparam>
        /// <param name="thing">the thing being converted</param>
        /// <param name="newThing">the converted thing</param>
        /// <returns>the actual value returned</returns>
        public static T TryConvert<T>(object thing, T def = default(T))
        {
            var newThing = def;

            try
            {
                if (thing != null)
                {
                    if (typeof(T).IsEnum)
                    {
                        if (thing is short || thing is int)
                            newThing = (T)thing;
                        else
                            newThing = (T)Enum.Parse(typeof(T), thing.ToString());
                    }
                    else
                        newThing = (T)Convert.ChangeType(thing, typeof(T));
                }
            }
            catch
            {
                //dont error on tryconvert, it's called tryconvert for a reason
                newThing = default(T);
            }

            return newThing;
        }
        #endregion

        public static string SerializeToJson(object thing)
        {
            var serializer = GetSerializer();

            var sb = new StringBuilder();
            var writer = new StringWriter(sb);

            serializer.Serialize(writer, thing);

            return sb.ToString();
        }

        public static int GenerateHashKey(List<object> parameters)
        {
            return GenerateHashKey(parameters.ToArray());
        }

        public static int GenerateHashKey(object[] parameters)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;

                foreach (object param in parameters)
                {
                    var stringCode = param == null ? 0 : param.GetHashCode();

                    hash = hash * 23 + stringCode;
                }

                return hash;
            }
        }


        private static JsonSerializer GetSerializer()
        {
            var serializer = JsonSerializer.Create();

            serializer.TypeNameHandling = TypeNameHandling.Auto;

            return serializer;
        }
    }
}

