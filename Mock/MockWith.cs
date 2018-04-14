using System;

namespace Logg.Mock
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false)]
    public class MockWith : Attribute
    {
    }
}

