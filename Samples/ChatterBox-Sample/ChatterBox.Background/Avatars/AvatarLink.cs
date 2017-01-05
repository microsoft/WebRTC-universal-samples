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
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;

namespace ChatterBox.Background.Avatars
{
    public static class AvatarLink
    {
        public static Uri CallCoordinatorUriFor(int avatar)
        {
            var fullPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Avatars");
            fullPath = Path.Combine(fullPath, $"{avatar}.jpg");
            return new Uri(fullPath, UriKind.Absolute);
        }

        public static string EmbeddedLinkFor(int avatar)
        {
            if (avatar >= 1 && avatar <= 16)
            {
                return $"ms-appdata:///local/Avatars/{avatar}.jpg";
            }
            return "ms-appdata:///local/Avatars/0.jpg";
        }


        public static IAsyncAction ExpandAvatarsToLocal()
        {
            return Task.Run(async () =>
            {
                var installationLocation = Package.Current.InstalledLocation;
                var assetsFolder = await installationLocation.GetFolderAsync("Assets");
                var avatarsFolder = await assetsFolder.GetFolderAsync("Avatars");
                var avatarFiles = await avatarsFolder.GetFilesAsync();

                var localAvatarDirectory =
                    await
                        ApplicationData.Current.LocalFolder.CreateFolderAsync("Avatars",
                            CreationCollisionOption.OpenIfExists);

                foreach (var avatarFile in avatarFiles)
                {
                    await avatarFile.CopyAsync(
                        localAvatarDirectory, avatarFile.Name, NameCollisionOption.ReplaceExisting);
                }
            }).AsAsyncAction();
        }
    }
}