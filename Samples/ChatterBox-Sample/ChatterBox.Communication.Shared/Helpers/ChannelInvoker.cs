//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace ChatterBox.Communication.Helpers
{
    public sealed class ChannelInvoker
    {
        /// <summary>
        ///     Helper used to invoke a method with an argument on a handler object
        /// </summary>
        /// <param name="handler">The object on which the method will be invoked</param>
        public ChannelInvoker(object handler)
        {
            Handler = handler;
        }

        private object Handler { get; }


        /// <summary>
        ///     Handles the request by deserializing it and invoking the requested method on the handler object
        /// </summary>
        public InvocationResult ProcessRequest(string request)
        {
            try
            {
                //Get the method name from the request
                var methodName = !request.Contains(" ")
                    ? request
                    : request.Substring(0, request.IndexOf(" ", StringComparison.CurrentCultureIgnoreCase));

                //Find the method on the handler
                var methods = Handler.GetType().GetRuntimeMethods();
                var method = methods.Single(s => s.Name == methodName);

                //Get the method parameters
                var parameters = method.GetParameters();

                object result;

                //If the method requires parameters, deserialize the parameter based on the required type
                if (parameters.Any())
                {
                    var paramsStartIndex = request.IndexOf(" ", StringComparison.CurrentCultureIgnoreCase);
                    object argument;
                    if (paramsStartIndex >= 0)
                    {
                        var serializedParameter = request.Substring(paramsStartIndex + 1);
                        argument = JsonConvert.DeserializeObject(serializedParameter, parameters.Single().ParameterType);
                    }
                    else
                    {
                        argument = null;
                    }
                    //Invoke the method on Handler and return the result
                    result = method.Invoke(Handler, new[] {argument});
                }
                else
                {
                    //Invoke the method on Handler and return the result
                    result = method.Invoke(Handler, null);
                }
                return new InvocationResult
                {
                    Invoked = true,
                    Result = result,
                    ResultType = method.ReturnType
                };
            }
            catch (Exception exception)
            {
                //Return a failed invocation result with the Exception message
                return new InvocationResult
                {
                    Invoked = false,
                    ErrorMessage = exception.ToString()
                };
            }
        }
    }
}