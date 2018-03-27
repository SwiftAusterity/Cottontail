using System;

namespace Cottontail.Mock
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class Mockable : Attribute
    {
        /// <summary>
        /// What type this is a mock of since it needs to be declared as dynamic
        /// </summary>
        public Type UnderlyingType { get; set; }

        public Mockable(Type underlyingType)
        {
            UnderlyingType = underlyingType;
        }
    }
}
