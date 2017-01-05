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
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ChatterBox.Communication.Helpers;
using Newtonsoft.Json;

namespace ChatterBox.Background.Helpers
{
    public static class AppServiceChannelHelper
    {
        /// <summary>
        ///     Invokes the method from the message on the handler object and sends back the return value
        /// </summary>
        public static async void HandleRequest(AppServiceRequest request, object handler, string message)
        {
            var invoker = new ChannelInvoker(handler);
            var result = invoker.ProcessRequest(message);
            await SendResponse(request, result);
        }

        /// <summary>
        ///     Sends a new message using the AppServiceConnection, containing the contract name, the method name and the
        ///     serialized argument
        /// </summary>
        public static IAsyncOperation<AppServiceResponse> InvokeChannelAsync(
            this AppServiceConnection connection,
            Type contractType,
            object argument,
            string method)
        {
            //Create a new instance of the channel writer and format the message (format: <Method> <Argument - can be null and is serialized as JSON>)
            var channelWriteHelper = new ChannelWriteHelper(contractType);
            var message = channelWriteHelper.FormatOutput(argument, method);

            //Send the message with the key being the contract name and the value being the serialized message
            return connection.SendMessageAsync(new ValueSet {{contractType.Name, message}});
        }


        /// <summary>
        ///     Sends a new message using the AppServiceConnection, containing the contract name, the method name and the
        ///     serialized argument.
        ///     The response is deserialized as a object based on the specified response type
        /// </summary>
        public static IAsyncOperation<object> InvokeChannelAsync(this AppServiceConnection connection, Type contractType,
            object argument,
            string method, Type responseType)
        {
            return Task.Run(async () =>
            {
                //Invoke the method across the channel, with the specified argument and return the value
                var resultMessage = await connection.InvokeChannelAsync(contractType, argument, method);

                //If there is no result, either the invoke failed or the invoked method is of type void
                if (resultMessage?.Status != AppServiceResponseStatus.Success) return null;
                if (!resultMessage.Message.Values.Any()) return null;

                //Deserialize the result and return the object
                return JsonConvert.DeserializeObject(resultMessage.Message.Values.Single().ToString(), responseType);
            }).AsAsyncOperation();
        }

        internal static T AwaitAsyncOperation<T>(IAsyncOperation<T> operation)
        {
            var operationTask = operation.AsTask();
            operationTask.Wait();
            return operationTask.Result;
        }


        /// <summary>
        ///     Serializes and sends the response for the AppServiceRequest
        /// </summary>
        private static async Task SendResponse(AppServiceRequest request, InvocationResult result)
        {
            if (result.Result == null) return;

            object response = null;

            var asyncAction = result.Result as IAsyncAction;
            if (asyncAction != null)
            {
                await asyncAction;
            }
            else
            {
                var awaitAsyncOperationMethod = typeof (AppServiceChannelHelper).GetRuntimeMethods()
                    .Single(method => method.Name == nameof(AwaitAsyncOperation));

                var awaitMethod = awaitAsyncOperationMethod.MakeGenericMethod(result.ResultType.GenericTypeArguments[0]);
                response = awaitMethod.Invoke(null, new[] {result.Result});
            }

            //Send a new ValueSet with the key as a random string and the value as the serialized result
            await request.SendResponseAsync(new ValueSet
            {
                {Guid.NewGuid().ToString(), JsonConvert.SerializeObject(response)}
            });
        }
    }
}