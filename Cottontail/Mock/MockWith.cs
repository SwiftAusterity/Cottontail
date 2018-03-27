using System;

namespace Cottontail.Mock
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false)]
    public class MockWith : Attribute
    {
    }
}

