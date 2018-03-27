using System;
using System.Linq;

namespace Cottontail.Mock
{
    public static class Mocker
    {
        public static T Mock<T>()
        {
            var toMock = typeof(T);

            var constructors = toMock.GetConstructors();
            var mockableConstructor = constructors.First(ctor => ctor.GetCustomAttributes(typeof(MockWith), false).Any());

            if (mockableConstructor == null)
                return (T)Activator.CreateInstance(toMock);

            //Must be an empty constructor
            T newObject = (T)mockableConstructor.Invoke(new object[] { });
            var props = toMock.GetProperties();

            foreach (var mockProp in props.Where(prop => prop.GetCustomAttributes(typeof(Mockable), false).Any()))
            {
                Mockable mockAttrib = (Mockable)mockProp.GetCustomAttributes(typeof(Mockable), false).First();
                mockProp.SetValue(newObject, new Mockery(mockAttrib.UnderlyingType), new object[] { });
            }

            return newObject;
        }
    }
}
