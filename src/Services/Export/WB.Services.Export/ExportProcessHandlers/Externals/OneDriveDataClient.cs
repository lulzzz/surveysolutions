﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using WB.Services.Infrastructure.Tenant;
using File = System.IO.File;

namespace WB.Services.Export.ExportProcessHandlers.Externals
{
    internal class OneDriveDataClient : IExternalDataClient
    {
        private readonly ILogger<OneDriveDataClient> logger;
        private IGraphServiceClient graphServiceClient;
        private TenantInfo tenant;

        private static long MaxAllowedFileSizeByMicrosoftGraphApi = 4 * 1024 * 1024;

        public OneDriveDataClient(
            ILogger<OneDriveDataClient> logger)
        {
            this.logger = logger;
        }

        public IDisposable InitializeDataClient(string accessToken, TenantInfo tenant)
        {
            this.tenant = tenant;

            logger.LogTrace("Creating Microsoft.Graph.Client for OneDrive file upload");

            graphServiceClient = new GraphServiceClient(new DelegateAuthenticationProvider(requestMessage =>
            {
                requestMessage
                    .Headers
                    .Authorization = new AuthenticationHeaderValue("bearer", accessToken);

                return Task.CompletedTask;
            }));

            return null;
        }

        private string Join(params string[] path) 
            => string.Join("/", path.Where( p => p != null));

        public Task<string> CreateApplicationFolderAsync(string subFolder)
            => Task.FromResult(Join("Survey Solutions", tenant.Name, subFolder));

        public Task<string> CreateFolderAsync(string folder, string parentFolder)
            => Task.FromResult(Join(parentFolder, folder));

        public async Task UploadFileAsync(string folder, string fileName, Stream fileStream, long contentLength, CancellationToken cancellationToken = default)
        {
            var item = graphServiceClient.Drive.Root.ItemWithPath(Join(folder, fileName));
            
            if (contentLength > MaxAllowedFileSizeByMicrosoftGraphApi)
            {
                logger.LogTrace("Uploading {fileName} to {folder}. Large file of size {Length} in chunks",
                    fileName, folder, contentLength);
                const int maxSizeChunk = 320 * 4 * 1024;
                
                var session = await item.CreateUploadSession().Request().PostAsync(cancellationToken);

                var temp = Path.GetTempFileName();
                var fs = File.Open(temp, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                try
                {
                    await fileStream.CopyToAsync(fs);

                    var chunkUploader = new ChunkedUploadProvider(session, graphServiceClient, fs, maxSizeChunk);
                    await chunkUploader.UploadAsync();
                }
                finally
                {
                    fs.Close();
                    File.Delete(temp);
                }
            }
            else
            {
                logger.LogTrace("Uploading {fileName} to {folder}. Small file of size {Length}", fileName, folder, contentLength);
                await item.Content.Request().PutAsync<DriveItem>(fileStream);
            }
        }

        public async Task<long?> GetFreeSpaceAsync()
        {
            var storageInfo = await graphServiceClient.Drive.Request().GetAsync();
            if (storageInfo?.Quota?.Total == null) return null;

            return storageInfo.Quota.Total - storageInfo.Quota.Used ?? 0;
        }
    }
}