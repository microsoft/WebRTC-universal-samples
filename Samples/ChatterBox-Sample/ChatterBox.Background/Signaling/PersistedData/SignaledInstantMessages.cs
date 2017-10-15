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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using ChatterBox.Communication.Messages.Relay;
using Newtonsoft.Json;

namespace ChatterBox.Background.Signalling.PersistedData
{
    public static class SignaledInstantMessages
    {
        private const string StorageFolder = "InstantMessages";
        private static readonly SemaphoreSlim AccessSemaphore = new SemaphoreSlim(1);

        public static IAsyncAction AddAsync(RelayMessage message)
        {
            return AddAsyncHelper(message).AsAsyncAction();
        }

        public static IAsyncOperation<bool> DeleteAsync(string messageId)
        {
            return DeleteAsyncHelper(messageId).AsAsyncOperation();
        }


        public static IAsyncOperation<IList<RelayMessage>> GetAllFromAsync(string userId)
        {
            return GetAllFromAsyncHelper(userId).AsAsyncOperation();
        }

        

        public static IAsyncOperation<bool> IsReceivedAsync(string messageId)
        {
            return IsReceivedAsyncHelper(messageId).AsAsyncOperation();
        }


        public static IAsyncAction ResetAsync()
        {
            return ResetAsyncHelper().AsAsyncAction();
        }

        private static async Task AddAsyncHelper(RelayMessage message)
        {
            Debug.Assert(message != null);
            Debug.Assert(message.Tag == RelayMessageTags.InstantMessage);
            var imFolder =
                await
                    ApplicationData.Current.LocalFolder.CreateFolderAsync(StorageFolder,
                        CreationCollisionOption.OpenIfExists);
            var imFile = await imFolder.CreateFileAsync(message.Id, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(imFile, JsonConvert.SerializeObject(message));
        }

        private static async Task<bool> DeleteAsyncHelper(string messageId)
        {
            try
            {
                await AccessSemaphore.WaitAsync();
                var imFolder =
                    await
                        ApplicationData.Current.LocalFolder.CreateFolderAsync(StorageFolder,
                            CreationCollisionOption.OpenIfExists);
                var imFile = await imFolder.GetFileAsync(messageId);
                await imFile.DeleteAsync();
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                AccessSemaphore.Release();
            }
            return true;
        }

        private static async Task<IList<RelayMessage>> GetAllFromAsyncHelper(string userId)
        {
            var result = new List<RelayMessage>();
            try
            {
                // Concurent access to storage files may fail.
                // FileIO.ReadTextAsync may throw exception:
                // "The handle with which this oplock was associated has been closed. The oplock is now broken. (Exception from HRESULT: 0x80070323)"
                await AccessSemaphore.WaitAsync();
                var imFolder =
                    await
                        ApplicationData.Current.LocalFolder.CreateFolderAsync(StorageFolder,
                            CreationCollisionOption.OpenIfExists);
                var imFiles = await imFolder.GetFilesAsync();
                foreach (var imFile in imFiles)
                {
                    var serializedIm = await FileIO.ReadTextAsync(imFile);
                    try
                    {
                        var msg = (RelayMessage) JsonConvert.DeserializeObject(serializedIm, typeof (RelayMessage));
                        if (msg.FromUserId == userId)
                        {
                            result.Add(msg);
                        }
                    }
                    catch (Exception)
                    {
                        // File may not be complete yet, just ignore.
                    }
                }
            }
            finally
            {
                AccessSemaphore.Release();
            }
            return result.ToArray();
        }

        private static async Task<bool> IsReceivedAsyncHelper(string messageId)
        {
            try
            {
                var imFolder =
                    await
                        ApplicationData.Current.LocalFolder.CreateFolderAsync(StorageFolder,
                            CreationCollisionOption.OpenIfExists);
                await imFolder.GetFileAsync(messageId);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private static async Task ResetAsyncHelper()
        {
            var imFolder =
                await
                    ApplicationData.Current.LocalFolder.CreateFolderAsync(StorageFolder,
                        CreationCollisionOption.OpenIfExists);
            await imFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
    }
}