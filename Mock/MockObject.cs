using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Utility;

namespace Logg.Mock
{
    /// <summary>
    /// A mock object
    /// </summary>
    public class Mockery : DynamicObject
    {
        private Type BaseType { get; set; }
        private Dictionary<string, object> MockedMembers { get; set; }
        private IList<Tuple<string, string, Func<object, bool>>> Conditions { get; set; }
        private Dictionary<string, Dictionary<string, object>> OutParameters { get; set; }
        private IList<Tuple<string, Type>> ExpectedErrors { get; set; }

        /// <summary>
        /// Create the mock object off of a type
        /// </summary>
        /// <param name="dataAccessorType">The type to mock</param>
        public Mockery(Type type)
        {
            MockedMembers = new Dictionary<string, object>();
            Conditions = new List<Tuple<string, string, Func<object, bool>>>();
            ExpectedErrors = new List<Tuple<string, Type>>();
            OutParameters = new Dictionary<string, Dictionary<string, object>>();

            BaseType = type;

            MakeAMockery(type);
        }

        #region Raw method upserts
        /// <summary>
        /// Add a method to the object, will remove the previously set method if there is one. Generates a randomized return object based on the type
        /// </summary>
        /// <param name="methodName">The method to mock</param>
        /// <param name="expectedReturnType">The return type to simulate</param>
        private void UpsertMethod(string methodName, Type expectedReturnType)
        {
            Func<object> del;

            if (expectedReturnType.IsClass && expectedReturnType != typeof(string))
            {
                del = () => RandomDataGenerator.GenerateRawTestValueForClass(expectedReturnType);
            }
            else
            {
                del = () => RandomDataGenerator.GenerateRawTestValue(expectedReturnType);
            }

            if (MockedMembers.ContainsKey(methodName))
                MockedMembers.Remove(methodName);

            MockedMembers.Add(methodName, del);
        }

        /// <summary>
        /// Add a method to the object, will remove the previously set method if there is one. Returns the initial value set
        /// </summary>
        /// <param name="methodName">The method to mock</param>
        /// <param name="returnValue">The actual value to return</param>
        private void UpsertMethod(string methodName, object returnValue, Dictionary<string, object> outParams)
        {
            Func<object> del = () => returnValue;

            if (MockedMembers.ContainsKey(methodName))
                MockedMembers.Remove(methodName);

            //Always remove them if we're overloading an existing mock
            if (OutParameters.ContainsKey(methodName))
                OutParameters.Remove(methodName);

            //Only add this if there are any to bother with
            if (outParams.Any())
                OutParameters.Add(methodName, outParams);

            MockedMembers.Add(methodName, del);
        }

        /// <summary>
        /// Add a method to the object, will remove the previously set method if there is one. Will only be used when the input parameters directly match the expectations.
        /// </summary>
        /// <param name="methodName">The method to mock</param>
        /// <param name="returnValue">The actual value to return</param>
        /// <param name="expectations">An array of parameters which much match to get this value back.</param>
        private void UpsertMethod(string methodName, IList<Tuple<string, string, Func<object, bool>>> conditions, IList<Tuple<string, Type>> expectedErrors, object returnValue, Dictionary<string, object> outParams)
        {
            var paramterizedMethodName = methodName;

            ExpectedErrors = ExpectedErrors.Union(expectedErrors).ToList();

            if (conditions.Any())
            {
                Conditions = Conditions.Union(conditions).ToList();
                paramterizedMethodName = string.Format("{0}_{1}", methodName, Serialization.GenerateHashKey(conditions.ToArray()));
            }

            UpsertMethod(paramterizedMethodName, returnValue, outParams);
        }
        #endregion

        #region DynamicObject overrides
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string name = binder.Name.ToLower();

            return MockedMembers.TryGetValue(name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            MockedMembers[binder.Name.ToLower()] = value;

            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var methodKeyName = binder.Name;
            var baseMethod = BaseType.GetMethod(binder.Name);
            result = null;

            //Do we meet all the conditions outlined for this method
            if (Conditions.Any(cond => cond.Item1.Equals(methodKeyName)))
            {
                var method = BaseType.GetMethods().First(m => m.Name.Equals(methodKeyName));

                if (method != null)
                {
                    var parameters = method.GetParameters();

                    int metConditions = 0;
                    for (int i = 0; i < parameters.Count(); i++)
                    {
                        var argument = parameters.ElementAt(i);
                        var value = args[i];

                        foreach (var condition in Conditions.Where(cond => cond.Item1.Equals(methodKeyName) && cond.Item2.Equals(argument.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (condition.Item3(value))
                                metConditions++;
                        }
                    }

                    if (metConditions.Equals(Conditions.Count(cond => cond.Item1.Equals(methodKeyName))))
                    {
                        //We have a match
                        methodKeyName = string.Format("{0}_{1}", binder.Name, Serialization.GenerateHashKey(Conditions.Where(cond => cond.Item1.Equals(methodKeyName)).ToArray()));
                    }
                }
            }

            //Do we want to throw an error?
            if (ExpectedErrors.Any(x => methodKeyName.Equals(x.Item1)))
            {
                Exception instance = (Exception)Activator.CreateInstance(ExpectedErrors.First(x => methodKeyName.Equals(x.Item1)).Item2);

                throw instance;
            }

            //Do we exist in the list of naked or conditional-hash-appended methods
            if (MockedMembers.ContainsKey(methodKeyName) && MockedMembers[methodKeyName] is Delegate)
            {
                Delegate del = MockedMembers[methodKeyName] as Delegate;

                result = del.DynamicInvoke();

                //Try and set out parameters
                if (OutParameters.ContainsKey(methodKeyName) && baseMethod.GetParameters().Any(param => param.IsOut))
                {
                    var outValues = OutParameters[methodKeyName];

                    int i = -1;
                    foreach (var param in baseMethod.GetParameters())
                    {
                        i++;

                        if (!param.IsOut || !outValues.ContainsKey(param.Name))
                            continue;

                        args[i] = (object)outValues[param.Name];
                    }
                }

                return true;
            }

            //We don't exist in the list at all just try to call the base method
            return base.TryInvokeMember(binder, args, out result);
        }
        #endregion

        #region Fluent Conditional interface
        public MockParameterBuilder ForMethod(string methodName)
        {
            var param = new MockParameterBuilder(methodName);

            param.OnReturn += new MockParameterBuilder.ReturnHandler(UpsertMethodFromEvent);

            return param;
        }

        private void UpsertMethodFromEvent(object pb, ReturnEventArgs args)
        {
            // Call the Event given subscribers
            UpsertMethod(args.methodName, args.conditionalValues, args.expectedErrors, args.returnValue, args.outParameters);
        }
        #endregion

        #region Helpers
        private void MakeAMockery(Type dataAccessorType)
        {
            var methods = dataAccessorType.GetMethods().Where(method => method.IsPublic);

            foreach (var method in methods)
            {
                UpsertMethod(method.Name, method.ReturnType);
            }
        }
        #endregion
    }

    /// <summary>
    /// Fluently constructs parameter conditionals and return values/conditions for the mock object
    /// </summary>
    public class MockParameterBuilder
    {
        private string Method { get; set; }
        private object Output { get; set; }

        //Used for logical conditions
        private IList<Tuple<string, string, Func<object, bool>>> Conditions { get; set; }
        private IList<Tuple<string, Type>> ExpectedErrors { get; set; }
        private Dictionary<string, object> OutParameters { get; set; }

        /// <summary>
        /// Start the expectation
        /// </summary>
        /// <param name="methodName">the name of the method to add to the mock</param>
        public MockParameterBuilder(string methodName)
        {
            Method = methodName;
            Conditions = new List<Tuple<string, string, Func<object, bool>>>();
            ExpectedErrors = new List<Tuple<string, Type>>();
            OutParameters = new Dictionary<string, object>();
        }

        #region parameter conditionals
        /// <summary>
        ///  Appends an expected condition to the When method for specific parameter on the method, wants the condition to return as true
        /// </summary>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="condition">expected condition to test</param>
        /// <returns>Fluent design</returns>
        public MockParameterBuilder IsTrue(string parameterName, Func<object, bool> condition)
        {
            return Expects(parameterName, condition);
        }

        /// <summary>
        /// Appends an expected condition to the When method for specific parameter on the method, wants the condition to return as false
        /// </summary>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="condition">expected condition to test</param>
        /// <returns>Fluent design</returns>
        public MockParameterBuilder IsFalse(string parameterName, Func<object, bool> condition)
        {
            return Expects(parameterName, (input) => !condition(input));
        }

        /// <summary>
        /// Appends an expected condition to the When method for specific parameter on the method, wants the input to equal the parameter value
        /// </summary>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="input">expected input to test against parameter</param>
        /// <returns>Fluent design</returns>
        public MockParameterBuilder IsEqualTo(string parameterName, object input)
        {
            return Expects(parameterName, (parameter) => parameter != null && input != null && parameter == input);
        }

        /// <summary>
        /// Appends an expected condition to the When method for specific parameter on the method, wants the input to be the same type as the parameter
        /// </summary>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="input">expected input to test against parameter</param>
        /// <returns>Fluent design</returns>
        public MockParameterBuilder IsAny(string parameterName, object input)
        {
            return Expects(parameterName, (parameter) => parameter != null && input != null && parameter.GetType() == input.GetType());
        }

        /// <summary>
        /// Appends an expected condition to the When method for specific parameter on the method, wants the parameter to be valid
        /// </summary>
        /// <param name="parameterName">Name of the parameter</param>
        /// <returns>Fluent design</returns>
        public MockParameterBuilder IsNotNull(string parameterName)
        {
            return Expects(parameterName, (parameter) => parameter != null);
        }

        /// <summary>
        /// Appends an expected condition to the When method for specific parameter on the method, wants the parameter to be null
        /// </summary>
        /// <param name="parameterName">Name of the parameter</param>
        /// <returns>Fluent design</returns>
        public MockParameterBuilder IsNull(string parameterName)
        {
            return Expects(parameterName, (parameter) => parameter == null);
        }

        //The base condition adder, wants the condition to return true
        private MockParameterBuilder Expects(string parameterName, Func<object, bool> condition)
        {
            Conditions.Add(new Tuple<string, string, Func<object, bool>>(Method, parameterName.ToString(), condition));

            return this;
        }
        #endregion

        #region End Results
        /// <summary>
        /// Sets the expected return value and adds the expectations to the mock
        /// </summary>
        /// <param name="value">return value to be returned</param>
        /// <param name="outParameters">out parameters to be set before returning by name and value</param>
        public MockParameterBuilder PassesOutParameter(string parameterName, object outParameter)
        {
            OutParameters.Add(parameterName, outParameter);

            return this;
        }

        /// <summary>
        /// Will cause the method to throw an exception instead of return
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public void ThrowsException(Type exception)
        {
            ExpectedErrors.Add(new Tuple<string, Type>(Method, exception));

            Output = null;

            OnReturnCommit(this, new ReturnEventArgs(Method, null, OutParameters, Conditions, ExpectedErrors));
        }

        /// <summary>
        /// Sets the expected return value and adds the expectations to the mock
        /// </summary>
        /// <param name="value">return value to be returned</param>
        public void Returns(object value)
        {
            Output = value;

            OnReturnCommit(this, new ReturnEventArgs(Method, Output, OutParameters, Conditions, ExpectedErrors));
        }

        /// <summary>
        /// Sets the expected return value of the method to null and adds the expectations to the mock
        /// </summary>      
        public void ReturnsNull()
        {
            Output = null;

            OnReturnCommit(this, new ReturnEventArgs(Method, null, OutParameters, Conditions, ExpectedErrors));
        }
        #endregion

        #region event handling
        public event ReturnHandler OnReturn;

        public delegate void ReturnHandler(object pb, ReturnEventArgs args);

        protected void OnReturnCommit(object pb, ReturnEventArgs args)
        {
            // Call the Event given subscribers
            OnReturn?.Invoke(pb, args);
        }
        #endregion
    }

    /// <summary>
    /// Handles event arguments for the ParameterBuilder-MockObject relationship
    /// </summary>
    public class ReturnEventArgs : EventArgs
    {
        public ReturnEventArgs(string name, object output, Dictionary<string, object> outList, IList<Tuple<string, string, Func<object, bool>>> conditions, IList<Tuple<string, Type>> faults)
        {
            methodName = name;
            returnValue = output;
            outParameters = outList;
            conditionalValues = conditions;
            expectedErrors = faults;
        }

        public readonly string methodName;
        public readonly IList<Tuple<string, string, Func<object, bool>>> conditionalValues;
        public readonly IList<Tuple<string, Type>> expectedErrors;
        public readonly object returnValue;
        public readonly Dictionary<string, object> outParameters;
    }
}
