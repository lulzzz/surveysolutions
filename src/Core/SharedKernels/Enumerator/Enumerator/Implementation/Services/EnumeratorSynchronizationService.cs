using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ncqrs.Eventing;
using WB.Core.GenericSubdomains.Portable.Implementation;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.Infrastructure.FileSystem;
using WB.Core.SharedKernel.Structures.Synchronization.SurveyManagement;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.Core.SharedKernels.DataCollection.Services;
using WB.Core.SharedKernels.DataCollection.WebApi;
using WB.Core.SharedKernels.Enumerator.Properties;
using WB.Core.SharedKernels.Enumerator.Services;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;
using WB.Core.SharedKernels.Enumerator.Services.Synchronization;
using WB.Core.SharedKernels.Questionnaire.Api;
using WB.Core.SharedKernels.Questionnaire.Translations;
using DownloadProgressChangedEventArgs = WB.Core.GenericSubdomains.Portable.Implementation.DownloadProgressChangedEventArgs;

namespace WB.Core.SharedKernels.Enumerator.Implementation.Services
{
    public abstract class EnumeratorSynchronizationService : ISynchronizationService, IAssignmentSynchronizationApi
    {
        protected abstract string ApiVersion { get; }
        protected abstract string ApiUrl { get; }

        protected string ApplicationUrl => string.Concat(ApiUrl, ApiVersion);
        
        protected string AuditLogController => string.Concat(ApplicationUrl, "/auditlog");
        protected string DevicesController => string.Concat(ApplicationUrl, "/devices");
        protected string UsersController => string.Concat(ApplicationUrl, "/users");
        protected virtual string InterviewsController => string.Concat(ApplicationUrl, "/interviews");

        protected string QuestionnairesController => string.Concat(ApplicationUrl, "/questionnaires");
        protected string AssignmentsController => string.Concat(ApplicationUrl, "/assignments");
        protected string TranslationsController => string.Concat(ApplicationUrl, "/translations");
        protected string AttachmentContentController => string.Concat(ApplicationUrl, "/attachments");
        
        protected string LogoUrl => string.Concat(ApplicationUrl, "/companyLogo");
        protected string AutoUpdateUrl => string.Concat(ApplicationUrl, "/autoupdate");
        
        protected string MapsController => string.Concat(ApplicationUrl, "/maps"); 

        private readonly IPrincipal principal;
        protected readonly IRestService restService;
        protected readonly IDeviceSettings deviceSettings;
        protected readonly ISyncProtocolVersionProvider syncProtocolVersionProvider;
        protected readonly IFileSystemAccessor fileSystemAccessor;
        protected readonly ICheckVersionUriProvider checkVersionUriProvider;
        protected readonly ILogger logger;
        protected readonly IEnumeratorSettings enumeratorSettings;

        protected RestCredentials restCredentials => this.principal.CurrentUserIdentity == null
            ? null
            : new RestCredentials {
                Login = this.principal.CurrentUserIdentity.Name,
                Token = this.principal.CurrentUserIdentity.Token };

        protected EnumeratorSynchronizationService(
            IPrincipal principal, 
            IRestService restService,
            IDeviceSettings deviceSettings, 
            ISyncProtocolVersionProvider syncProtocolVersionProvider,
            IFileSystemAccessor fileSystemAccessor,
            ICheckVersionUriProvider checkVersionUriProvider,
            ILogger logger,
            IEnumeratorSettings enumeratorSettings)
        {
            this.principal = principal;
            this.restService = restService;
            this.deviceSettings = deviceSettings;
            this.syncProtocolVersionProvider = syncProtocolVersionProvider;
            this.fileSystemAccessor = fileSystemAccessor;
            this.checkVersionUriProvider = checkVersionUriProvider;
            this.logger = logger;
            this.enumeratorSettings = enumeratorSettings;
        }

        #region [User Api]

        public async Task<string> LoginAsync(LogonInfo logonInfo, RestCredentials credentials, CancellationToken? token = null)
        {
            try
            {
                var authToken = await this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync<string>(
                    url: string.Concat(this.UsersController, "/login"),
                    request: logonInfo,
                    credentials: credentials,
                    token: token));

                return authToken;
            }
            catch (RestException ex)
            {
                throw this.CreateSynchronizationExceptionByRestException(ex);
            }
        }

        public Task<Guid> GetCurrentSupervisor(CancellationToken token, RestCredentials credentials)
        {
            return this.TryGetRestResponseOrThrowAsync(() =>
                this.restService.GetAsync<Guid>(url: string.Concat(this.UsersController, "/supervisor"),
                    credentials: credentials ?? this.restCredentials, token: token));
        }

        public Task<bool> IsAutoUpdateEnabledAsync(CancellationToken token)
            => this.TryGetRestResponseOrThrowAsync(() =>
                this.restService.GetAsync<bool>(url: AutoUpdateUrl, credentials: this.restCredentials, token: token));

        public Task UploadAuditLogEntityAsync(AuditLogEntitiesApiView entities, CancellationToken cancellationToken)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: $"{this.AuditLogController}",
                request: entities,
                credentials: this.restCredentials,
                token: cancellationToken));
        }

        public Task<List<Guid>> CheckObsoleteInterviewsAsync(List<ObsoletePackageCheck> checks, CancellationToken cancellationToken)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync<List<Guid>>(
                url: string.Concat(InterviewsController, "/CheckObsoleteInterviews"),
                request: checks,
                credentials: this.restCredentials,
                token: cancellationToken));
        }

        #endregion

        #region AssignmentsApi

        public Task<List<AssignmentApiView>> GetAssignmentsAsync(CancellationToken cancellationToken)
        {
            var response = this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<List<AssignmentApiView>>(
                url: this.AssignmentsController, credentials: this.restCredentials, token: cancellationToken));

            return response;
        }

        public Task<AssignmentApiDocument> GetAssignmentAsync(int id, CancellationToken cancellationToken)
        {
            var response = this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<AssignmentApiDocument>(
                url: $"{this.AssignmentsController}/{id}", credentials: this.restCredentials, token: cancellationToken));

            return response;
        }

        #endregion

        #region [Device Api]

        public Task<bool> HasCurrentUserDeviceAsync(RestCredentials credentials = null, CancellationToken? token = null)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<bool>(url: string.Concat(this.UsersController, "/hasdevice"),
                credentials: credentials ?? this.restCredentials, token: token));
        }

        public async Task CanSynchronizeAsync(RestCredentials credentials = null, CancellationToken? token = null)
        {
            string url = string.Concat(ApiUrl, "compatibility/", this.deviceSettings.GetDeviceId(), "/",
                this.syncProtocolVersionProvider.GetProtocolVersion());

            var response = await this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<string>(
                url: url, credentials: credentials ?? this.restCredentials, token: token)).ConfigureAwait(false);

            if (response == null || response.Trim('"') != CanSynchronizeValidResponse)
            {
                throw new SynchronizationException(SynchronizationExceptionType.InvalidUrl, InterviewerUIResources.InvalidEndpoint);
            }
        }

        protected abstract string CanSynchronizeValidResponse { get; }

        public Task SendDeviceInfoAsync(DeviceInfoApiView info, CancellationToken? token = null)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: $"{this.DevicesController}/info",
                request: info,
                credentials: this.restCredentials,
                token: token));
        }

        public Task SendSyncStatisticsAsync(SyncStatisticsApiView statistics, CancellationToken token, RestCredentials credentials)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: $"{this.DevicesController}/statistics",
                request: statistics,
                credentials: this.restCredentials,
                token: token));
        }

        public Task SendUnexpectedExceptionAsync(UnexpectedExceptionApiView exception, CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: $"{this.DevicesController}/exception",
                request: exception,
                credentials: this.restCredentials,
                token: token));
        }

        public Task LinkCurrentUserToDeviceAsync(RestCredentials credentials = null, CancellationToken? token = null)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: string.Concat(this.DevicesController, "/link/", this.deviceSettings.GetDeviceId(), "/", this.syncProtocolVersionProvider.GetProtocolVersion()),
                credentials: credentials ?? this.restCredentials, token: token));
        }

        #endregion

        #region [Questionnaire Api]


        public Task<byte[]> GetQuestionnaireAssemblyAsync(QuestionnaireIdentity questionnaire, Action<decimal, long, long> onDownloadProgressChanged,
            CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(async () =>
            {
                var restFile = await this.restService.DownloadFileAsync(
                    url: $"{this.QuestionnairesController}/{questionnaire.QuestionnaireId}/{questionnaire.Version}/assembly",
                    onDownloadProgressChanged: ToDownloadProgressChangedEvent(onDownloadProgressChanged),
                    token: token,
                    credentials: this.restCredentials).ConfigureAwait(false);
                return restFile.Content;
            });
        }

        public Task<QuestionnaireApiView> GetQuestionnaireAsync(QuestionnaireIdentity questionnaire, Action<decimal, long, long> onDownloadProgressChanged, CancellationToken token)
        {
            var questionnaireContentVersion = this.enumeratorSettings.GetSupportedQuestionnaireContentVersion().Major;

            return this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<QuestionnaireApiView>(
                url: $"{this.QuestionnairesController}/{questionnaire.QuestionnaireId}/{questionnaire.Version}/{questionnaireContentVersion}",
                onDownloadProgressChanged: ToDownloadProgressChangedEvent(onDownloadProgressChanged),
                token: token,
                credentials: this.restCredentials));
        }
        
        public Task<List<QuestionnaireIdentity>> GetCensusQuestionnairesAsync(CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<List<QuestionnaireIdentity>>(
                url: string.Concat(this.QuestionnairesController, "/census"),
                credentials: this.restCredentials, token: token));
        }

        public Task<List<QuestionnaireIdentity>> GetServerQuestionnairesAsync(CancellationToken cancellationToken)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<List<QuestionnaireIdentity>>(
                url: string.Concat(this.QuestionnairesController, "/list"), credentials: this.restCredentials, token: cancellationToken));
        }
      
        public Task<List<MapView>> GetMapList(CancellationToken cancellationToken)
        {
            return  this.TryGetRestResponseOrThrowAsync(() => this.restService.GetAsync<List<MapView>>(
                url: this.MapsController, token: cancellationToken, credentials: this.restCredentials));
        }
        
        public Task<RestStreamResult> GetMapContentStream(string mapName, CancellationToken cancellationToken)
        {
            return this.TryGetRestResponseOrThrowAsync(async () => 
                await this.restService.GetResponseStreamAsync(
                    url: $"{this.MapsController}/details",
                    queryString: new {id = WebUtility.UrlEncode(mapName)},
                    token: cancellationToken,
                    credentials: this.restCredentials).ConfigureAwait(false));
        }

        public Task<List<TranslationDto>> GetQuestionnaireTranslationAsync(QuestionnaireIdentity questionnaireIdentity, CancellationToken cancellationToken)
        {
            var url = $"{this.TranslationsController}/{questionnaireIdentity}";

            return this.TryGetRestResponseOrThrowAsync(() =>  this.restService.GetAsync<List<TranslationDto>>(
                url: url,
                credentials: this.restCredentials, token: cancellationToken));
        }

        public Task LogQuestionnaireAsSuccessfullyHandledAsync(QuestionnaireIdentity questionnaire)
        {
            return this.TryGetRestResponseOrThrowAsync(async () => await this.restService.PostAsync(
                url: string.Concat(this.QuestionnairesController, "/", questionnaire.QuestionnaireId, "/", questionnaire.Version, "/logstate"),
                credentials: this.restCredentials));
        }

        public Task LogQuestionnaireAssemblyAsSuccessfullyHandledAsync(QuestionnaireIdentity questionnaire)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: string.Concat(this.QuestionnairesController, "/", questionnaire.QuestionnaireId, "/", questionnaire.Version, "/assembly/logstate"),
                credentials: this.restCredentials));
        }
        
        #endregion

        #region [Interview Api]

        public Task<List<InterviewApiView>> GetInterviewsAsync(CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() =>
                this.restService.GetAsync<List<InterviewApiView>>(url: this.InterviewsController,
                    credentials: this.restCredentials, token: token));
        }

        public Task LogInterviewAsSuccessfullyHandledAsync(Guid interviewId)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: string.Concat(this.InterviewsController, "/", interviewId, "/logstate"),
                credentials: this.restCredentials));
        }

        public Task<List<CommittedEvent>> GetInterviewDetailsAsync(Guid interviewId, Action<decimal, long, long> onDownloadProgressChanged, CancellationToken token)
        {
            try
            {
                return this.TryGetRestResponseOrThrowAsync(
                    () =>  this.restService.GetAsync<List<CommittedEvent>>(
                        url: string.Concat(this.InterviewsController, "/", interviewId),
                        credentials: this.restCredentials,
                        onDownloadProgressChanged: ToDownloadProgressChangedEvent(onDownloadProgressChanged),
                        token: token));
            }
            catch (SynchronizationException exception)
            {
                var httpStatusCode = (exception.InnerException as RestException)?.StatusCode;
                if (httpStatusCode == HttpStatusCode.NotFound)
                    return Task.FromResult<List<CommittedEvent>>(null);

                this.logger.Error("Exception on download interview. ID:" + interviewId, exception);
                throw;
            }
        }

        public Task UploadInterviewAsync(Guid interviewId, InterviewPackageApiView completedInterview, Action<decimal, long, long> onDownloadProgressChanged, CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: string.Concat(this.InterviewsController, "/", interviewId),
                request: completedInterview,
                credentials: this.restCredentials,
                token: token));
        }

        public Task UploadInterviewImageAsync(Guid interviewId, string fileName, byte[] fileData, Action<decimal, long, long> onDownloadProgressChanged, CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: string.Concat(this.InterviewsController, "/", interviewId, "/image"),
                request: new PostFileRequest
                {
                    InterviewId = interviewId,
                    FileName = fileName,
                    Data = Convert.ToBase64String(fileData)
                },
                credentials: this.restCredentials,
                token: token));
        }

        public Task UploadInterviewAudioAsync(Guid interviewId, string fileName, string contentType, byte[] fileData, Action<decimal, long, long> onDownloadProgressChanged,
            CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() => this.restService.PostAsync(
                url: string.Concat(this.InterviewsController, "/", interviewId, "/audio"),
                request: new PostFileRequest
                {
                    InterviewId = interviewId,
                    FileName = fileName,
                    ContentType = contentType,
                    Data = Convert.ToBase64String(fileData)
                },
                credentials: this.restCredentials,
                token: token));
        }

        #endregion
        
        #region Attachments
        public Task<List<string>> GetAttachmentContentsAsync(QuestionnaireIdentity questionnaire, 
            Action<decimal, long, long> onDownloadProgressChanged,
            CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(() =>
                this.restService.GetAsync<List<string>>(
                    url: $"{this.QuestionnairesController}/{questionnaire.QuestionnaireId}/{questionnaire.Version}/attachments",
                    credentials: this.restCredentials,
                    token: token));
        }

        public Task<AttachmentContent> GetAttachmentContentAsync(string contentId, 
            Action<decimal, long, long> onDownloadProgressChanged, 
            CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(async () =>
            {
                var restFile = await this.restService.DownloadFileAsync(
                    url: $"{this.AttachmentContentController}/{contentId}",
                    onDownloadProgressChanged: ToDownloadProgressChangedEvent(onDownloadProgressChanged),
                    token: token,
                    credentials: this.restCredentials).ConfigureAwait(false);

                var attachmentContent = new AttachmentContent()
                {
                    Content = restFile.Content,
                    ContentType = restFile.ContentType,
                    Id = restFile.ContentHash.Trim('"'),
                    Size = restFile.ContentLength ?? restFile.Content.Length
                };
                return attachmentContent;
            });
        }
        #endregion

        #region [Application Api]
        public Task<byte[]> GetApplicationAsync(CancellationToken token, Action<DownloadProgressChangedEventArgs> onDownloadProgressChanged = null) => 
            this.TryGetRestResponseOrThrowAsync(async () =>
            {
                var restFile = await this.restService.DownloadFileAsync(
                    url: this.checkVersionUriProvider.CheckVersionUrl, token: token,
                    credentials: this.restCredentials, 
                    onDownloadProgressChanged: onDownloadProgressChanged);

                return restFile.Content;
            });

        public Task<byte[]> GetApplicationPatchAsync(CancellationToken token, Action<DownloadProgressChangedEventArgs> onDownloadProgressChanged)
        {
            return this.TryGetRestResponseOrThrowAsync(async () =>
            {
                var interviewerPatchApiUrl = $"{this.checkVersionUriProvider.CheckVersionUrl}patch/{this.deviceSettings.GetApplicationVersionCode()}";

                var restFile = await this.restService.DownloadFileAsync(url: interviewerPatchApiUrl, 
                    token: token,
                    credentials: this.restCredentials,
                    onDownloadProgressChanged: onDownloadProgressChanged);

                return restFile.Content;
            });
        }

        public Task<int?> GetLatestApplicationVersionAsync(CancellationToken token)
        {
            return this.TryGetRestResponseOrThrowAsync(async () =>
                await this.restService.GetAsync<int?>(
                    url: string.Concat(this.checkVersionUriProvider.CheckVersionUrl, "latestversion"),
                    credentials: this.restCredentials, token: token).ConfigureAwait(false));
        }

        public Task SendBackupAsync(string filePath, CancellationToken token)
        {
            var backupHeaders = new Dictionary<string, string>()
            {
                { "DeviceId", this.deviceSettings.GetDeviceId() },
            };

            return this.TryGetRestResponseOrThrowAsync(async () =>
            {
                using (var fileStream = this.fileSystemAccessor.ReadFile(filePath))
                {
                    await this.restService.SendStreamAsync(
                        stream: fileStream,
                        customHeaders: backupHeaders,
                        url: string.Concat(ApplicationUrl, "/tabletInfo"),
                        credentials: this.restCredentials,
                        token: token).ConfigureAwait(false);
                }
            });
        }

        #endregion

        protected async Task TryGetRestResponseOrThrowAsync(Func<Task> restRequestTask)
        {
            if (restRequestTask == null) throw new ArgumentNullException(nameof(restRequestTask));

            try
            {
                await restRequestTask().ConfigureAwait(false);
            }
            catch (RestException ex)
            {
                throw this.CreateSynchronizationExceptionByRestException(ex);
            }
        }

        protected async Task<T> TryGetRestResponseOrThrowAsync<T>(Func<Task<T>> restRequestTask)
        {
            if (restRequestTask == null) throw new ArgumentNullException(nameof(restRequestTask));

            try
            {
                return await restRequestTask().ConfigureAwait(false);
            }
            catch (RestException ex)
            {
                throw this.CreateSynchronizationExceptionByRestException(ex);
            }
        }

        private SynchronizationException CreateSynchronizationExceptionByRestException(RestException ex)
        {
            string exceptionMessage = InterviewerUIResources.UnexpectedException;
            SynchronizationExceptionType exceptionType = SynchronizationExceptionType.Unexpected;
            switch (ex.Type)
            {
                case RestExceptionType.RequestByTimeout:
                    exceptionMessage = InterviewerUIResources.RequestTimeout;
                    exceptionType = SynchronizationExceptionType.RequestByTimeout;
                    break;
                case RestExceptionType.RequestCanceledByUser:
                    exceptionMessage = InterviewerUIResources.RequestCanceledByUser;
                    exceptionType = SynchronizationExceptionType.RequestCanceledByUser;
                    break;
                case RestExceptionType.HostUnreachable:
                    exceptionMessage = InterviewerUIResources.HostUnreachable;
                    exceptionType = SynchronizationExceptionType.HostUnreachable;
                    break;
                case RestExceptionType.InvalidUrl:
                    exceptionMessage = InterviewerUIResources.InvalidEndpoint;
                    exceptionType = SynchronizationExceptionType.InvalidUrl;
                    break;
                case RestExceptionType.NoNetwork:
                    exceptionMessage = InterviewerUIResources.NoNetwork;
                    exceptionType = SynchronizationExceptionType.NoNetwork;
                    break;
                case RestExceptionType.UnacceptableCertificate:
                    exceptionMessage = InterviewerUIResources.UnacceptableSSLCertificate;
                    exceptionType = SynchronizationExceptionType.UnacceptableSSLCertificate;
                    break;
                case RestExceptionType.Unexpected:
                    switch (ex.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            if (ex.Message.Contains("lock"))
                            {
                                exceptionMessage = InterviewerUIResources.AccountIsLockedOnServer;
                                exceptionType = SynchronizationExceptionType.UserLocked;
                            }
                            else if (ex.Message.Contains("not approved"))
                            {
                                exceptionMessage = InterviewerUIResources.AccountIsNotApprovedOnServer;
                                exceptionType = SynchronizationExceptionType.UserNotApproved;
                            }
                            else if (ex.Message.Contains("not have an interviewer role"))
                            {
                                exceptionMessage = InterviewerUIResources.AccountIsNotAnInterviewer;
                                exceptionType = SynchronizationExceptionType.UserIsNotInterviewer;
                            }
                            else
                            {
                                exceptionMessage = InterviewerUIResources.Unauthorized;
                                exceptionType = SynchronizationExceptionType.Unauthorized;
                            }
                            break;
                        case HttpStatusCode.ServiceUnavailable:
                            var isMaintenance = ex.Message.Contains("maintenance");

                            if (isMaintenance)
                            {
                                exceptionMessage = InterviewerUIResources.Maintenance;
                                exceptionType = SynchronizationExceptionType.Maintenance;
                            }
                            else
                            {
                                this.logger.Warn("Server error", ex);

                                exceptionMessage = InterviewerUIResources.ServiceUnavailable;
                                exceptionType = SynchronizationExceptionType.ServiceUnavailable;
                            }
                            break;
                        case HttpStatusCode.NotAcceptable:
                            exceptionMessage = InterviewerUIResources.NotSupportedServerSyncProtocolVersion;
                            exceptionType = SynchronizationExceptionType.NotSupportedServerSyncProtocolVersion;
                            break;
                        case HttpStatusCode.UpgradeRequired:
                            exceptionMessage = InterviewerUIResources.UpgradeRequired;
                            exceptionType = SynchronizationExceptionType.UpgradeRequired;
                            break;
                        case HttpStatusCode.BadRequest:
                        case HttpStatusCode.Redirect:
                            exceptionMessage = InterviewerUIResources.InvalidEndpoint;
                            exceptionType = SynchronizationExceptionType.InvalidUrl;
                            break;
                        case HttpStatusCode.NotFound:
                            this.logger.Warn("Server error", ex);
                            exceptionMessage = InterviewerUIResources.InvalidEndpoint;
                            exceptionType = SynchronizationExceptionType.InvalidUrl;
                            break;
                        case HttpStatusCode.InternalServerError:
                            this.logger.Warn("Server error", ex);

                            exceptionMessage = InterviewerUIResources.InternalServerError;
                            exceptionType = SynchronizationExceptionType.InternalServerError;
                            break;
                        case HttpStatusCode.Forbidden:
                            exceptionType = SynchronizationExceptionType.UserLinkedToAnotherDevice;
                            break;
                    }
                    break;
            }

            return new SynchronizationException(exceptionType, exceptionMessage, ex);
        }

        private static Action<DownloadProgressChangedEventArgs> ToDownloadProgressChangedEvent(Action<decimal, long, long> onDownloadProgressChanged)
        {
            void OnDownloadProgressChangedInternal(DownloadProgressChangedEventArgs args)
            {
                onDownloadProgressChanged?.Invoke(args.ProgressPercentage, args.BytesReceived, args.TotalBytesToReceive ?? 0);
            }

            return OnDownloadProgressChangedInternal;
        }

        public async Task<CompanyLogoInfo> GetCompanyLogo(string storedClientEtag, CancellationToken cancellationToken)
        {
            var response = await this.TryGetRestResponseOrThrowAsync(() => this.restService.DownloadFileAsync(
                url: this.LogoUrl,
                credentials: this.restCredentials,
                customHeaders: !string.IsNullOrEmpty(storedClientEtag) ? new Dictionary<string, string> {{"If-None-Match", storedClientEtag }} : null,
                token: cancellationToken)).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NoContent)
                return new CompanyLogoInfo {HasCustomLogo = false};
            if (response.StatusCode == HttpStatusCode.NotModified)
                return new CompanyLogoInfo {LogoNeedsToBeUpdated = false, HasCustomLogo = true};

            return new CompanyLogoInfo
            {
                HasCustomLogo = true,
                LogoNeedsToBeUpdated = true,
                Logo = response.Content,
                Etag = response.ContentHash
            };
        }
    }
}