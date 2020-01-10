﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WB.Core.BoundedContexts.Headquarters.DataExport;
using WB.Core.BoundedContexts.Headquarters.DataExport.Dtos;
using WB.Core.BoundedContexts.Headquarters.DataExport.Security;
using WB.Core.BoundedContexts.Headquarters.DataExport.Services;
using WB.Core.BoundedContexts.Headquarters.DataExport.Views;
using WB.Core.BoundedContexts.Headquarters.Factories;
using WB.Core.BoundedContexts.Headquarters.Services;
using WB.Core.BoundedContexts.Headquarters.Views.Questionnaire;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.Infrastructure.FileSystem;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.Core.SharedKernels.DataCollection.ValueObjects.Interview;
using WB.UI.Headquarters.Filters;

namespace WB.UI.Headquarters.API
{
    [ApiValidationAntiForgeryToken]
    [Authorize(Roles = "Administrator, Headquarter")]
    [Route("api/[controller]/[action]")]
    [ResponseCache(NoStore = true)]
    public class DataExportApiController : ControllerBase
    {
        private readonly IFileSystemAccessor fileSystemAccessor;
        private readonly IDataExportStatusReader dataExportStatusReader;
        private readonly IExportFileNameService exportFileNameService;
        private readonly IExportServiceApi exportServiceApi;
        private readonly IExportSettings exportSettings;
        private readonly IQuestionnaireBrowseViewFactory questionnaireBrowseViewFactory;
        private readonly ISystemLog auditLog;
        private readonly ISerializer serializer;
        private readonly ExternalStoragesSettings externalStoragesSettings;

        public DataExportApiController(
            IFileSystemAccessor fileSystemAccessor,
            IDataExportStatusReader dataExportStatusReader,
            ISerializer serializer,
            IExportSettings exportSettings,
            IQuestionnaireBrowseViewFactory questionnaireBrowseViewFactory,
            IExportFileNameService exportFileNameService,
            IExportServiceApi exportServiceApi,
            ISystemLog auditLog, ExternalStoragesSettings externalStoragesSettings)
        {
            this.fileSystemAccessor = fileSystemAccessor;
            this.dataExportStatusReader = dataExportStatusReader;
            this.exportSettings = exportSettings;
            this.questionnaireBrowseViewFactory = questionnaireBrowseViewFactory;
            this.exportFileNameService = exportFileNameService;
            this.exportServiceApi = exportServiceApi;
            this.auditLog = auditLog;
            this.externalStoragesSettings = externalStoragesSettings;
            this.serializer = serializer;
        }

        [HttpGet]
        [ObserverNotAllowed]
        public async Task<ActionResult<List<long>>> GetRunningJobs()
        {
            try
            {
                return (await this.exportServiceApi.GetRunningExportJobs()).OrderByDescending(x => x).ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        [HttpGet]
        [ObserverNotAllowed]
        public async Task<ActionResult<List<ExportStatusItem>>> ExportStatus()
        {
            try
            {
                var jobs = (await this.exportServiceApi.GetAllJobsList()).OrderByDescending(x => x).ToList();
                var runningJobs = (await this.exportServiceApi.GetRunningExportJobs());

                var allJobs = jobs.Union(runningJobs).ToList();

                var result = new List<ExportStatusItem>();

                foreach (var job in allJobs)
                {
                    result.Add(new ExportStatusItem
                    {
                        Id = job,
                        Running = runningJobs.Contains(job)
                    });
                }

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public class ExportStatusItem
        {
            public long Id { get; set; }
            public bool Running { get; set; }
        }

        [HttpGet]
        [ObserverNotAllowed]
        public async Task<ActionResult<List<DataExportProcessView>>> Status([FromQuery(Name = "id[]")] long[] ids)
        {
            var query = new TransformBlock<long, DataExportProcessView>(
                async id =>
                {
                    try
                    {
                        return await dataExportStatusReader.GetProcessStatus(id);
                    }
                    catch
                    {
                        return null;
                    }
                });

            var buffer = new BufferBlock<DataExportProcessView>();
            using var _ = query.LinkTo(buffer, new DataflowLinkOptions {PropagateCompletion = true});
            
            foreach (var id in ids) query.Post(id);

            query.Complete();
            await query.Completion;

            var result = new List<DataExportProcessView>();

            while (buffer.TryReceive(f => f != null, out var item))
            {
                result.Add(item);
            }

            return result;
        }

        [HttpGet]
        [ObserverNotAllowed]
        public async Task<ActionResult<ExportDataAvailabilityView>> DataAvailability(Guid id, long version)
        {
            ExportDataAvailabilityView result = await dataExportStatusReader.GetDataAvailabilityAsync(new QuestionnaireIdentity(id, version));
            if (result == null)
            {
                return NotFound();
            }

            return result;
        }

        [HttpGet]
        [ObserverNotAllowed]
        public async Task<ActionResult<bool>> WasExportFileRecreated(long id)
        {
            bool result = await dataExportStatusReader.WasExportFileRecreated(id);
            return result;
        }

        [HttpGet]
        [ObserverNotAllowed]
        
        public async Task<ActionResult> DownloadData(long id)
        {
            var processView = await dataExportStatusReader.GetProcessStatus(id);
            if (processView == null)
            {
                return NotFound();
            }

            return await this.AllData(
                processView.QuestionnaireIdentity.QuestionnaireId,
                processView.QuestionnaireIdentity.Version,
                processView.Format,
                processView.InterviewStatus,
                processView.FromDate,
                processView.ToDate);
        }

        [HttpGet]
        [ObserverNotAllowed]
        
        public async Task<ActionResult> AllData(Guid id, long version, DataExportFormat format,
            InterviewStatus? status = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            DataExportArchive result = await this.dataExportStatusReader.GetDataArchive(new QuestionnaireIdentity(id, version), format, status, from, to);
            if (result == null)
            {
                return NotFound();
            }
            else if (result.Redirect != null)
            {
                return Redirect(result.Redirect);
            }
            else
            {
                return File(result.Data, @"applications/octet-stream", WebUtility.UrlDecode(result.FileName));
            }
        }

        [HttpGet]
        [ObserverNotAllowed]
        
        public async Task<ActionResult> DDIMetadata(Guid id, long version)
        {
            var questionnaireIdentity = new QuestionnaireIdentity(id, version);

            var archivePassword = this.exportSettings.EncryptionEnforced() ? this.exportSettings.GetPassword() : null;
            var result = await exportServiceApi.GetDdiArchive(questionnaireIdentity.ToString(), archivePassword);

            var fileName = this.exportFileNameService.GetFileNameForDdiByQuestionnaire(questionnaireIdentity);

            return File(await result.ReadAsStreamAsync(), "text/xml", fileSystemAccessor.GetFileName(fileName));
        }

        [HttpPost]
        [ObserverNotAllowed]
        public async Task<ActionResult<DataExportUpdateRequestResult>> Regenerate(long id, string accessToken = null)
        {
            var result = await this.exportServiceApi.Regenerate(id, GetPasswordFromSettings(), null);
            return result;
        }

        [HttpPost]
        [ObserverNotAllowed]
        public async Task<ActionResult<long>> RequestUpdate(Guid id, long version,
            DataExportFormat format, InterviewStatus? status = null, DateTime? from = null, DateTime? to = null)
        {
            var questionnaireIdentity = new QuestionnaireIdentity(id, version);

            var questionnaireBrowseItem = this.questionnaireBrowseViewFactory.GetById(questionnaireIdentity);
            if (questionnaireBrowseItem == null)
                return NotFound("Questionnaire not found");

            return await RequestExportUpdateAsync(questionnaireBrowseItem, format, status, @from, to);
        }

        private async Task<ActionResult<long>> RequestExportUpdateAsync(
            QuestionnaireBrowseItem questionnaireBrowseItem,
            DataExportFormat format,
            InterviewStatus? status,
            DateTime? @from,
            DateTime? to,
            string accessToken = null,
            ExternalStorageType? externalStorageType = null)
        {
            long jobId = 0;
            try
            {
                DataExportUpdateRequestResult result = await this.exportServiceApi.RequestUpdate(
                    questionnaireBrowseItem.Id,
                    format,
                    status,
                    @from?.ToUniversalTime(),
                    to?.ToUniversalTime(),
                    GetPasswordFromSettings(),
                    accessToken,
                    externalStorageType);

                jobId = result?.JobId ?? 0;

                this.auditLog.ExportStared(
                    $@"{questionnaireBrowseItem.Title} v{questionnaireBrowseItem.Version} {status?.ToString() ?? ""}",
                    format);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }

            return jobId;
        }

        [HttpPost]
        [ObserverNotAllowed]
        public async Task<ActionResult<bool>> DeleteDataExportProcess([FromQuery] long id)
        {
            try
            {
                await this.exportServiceApi.DeleteProcess(id);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [HttpPost]
        [ObserverNotAllowed]
        public Task<DataExportStatusView> GetExportStatus(Guid id, long version, InterviewStatus? status, DateTime? from = null, DateTime? to = null)
            => this.dataExportStatusReader.GetDataExportStatusForQuestionnaireAsync(new QuestionnaireIdentity(id, version),
                status,
                fromDate: @from?.ToUniversalTime(),
                toDate: to?.ToUniversalTime());

        /// <summary>
        /// Handle CORS preflight request
        /// </summary>
        /// <returns></returns>
        [HttpOptions]
        [AllowAnonymous]
        [Localizable(false)]
        public ActionResult ExportToExternalStorage()
        {
            var uri = new Uri(externalStoragesSettings.OAuth2.RedirectUri);
            
            // Define and add values to variables: origins, headers, methods (can be global)               
            Response.Headers.Add("Access-Control-Allow-Origin", $"{uri.Scheme}://{uri.Host}");
            Response.Headers.Add("Access-Control-Allow-Methods", "POST");
            
            return Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> ExportToExternalStorage(ExportToExternalStorageModel model)
        {
            var state = this.serializer.Deserialize<ExternalStorageStateModel>(model.State);
            if (state == null)
                return BadRequest("Export parameters not found");

            var questionnaireBrowseItem = this.questionnaireBrowseViewFactory.GetById(state.QuestionnaireIdentity);
            if (questionnaireBrowseItem == null || questionnaireBrowseItem.IsDeleted)
                return NotFound("@Questionnaire not found");

            await RequestExportUpdateAsync(questionnaireBrowseItem,
                state.Format ?? DataExportFormat.Binary,
                state.InterviewStatus,
                state.FromDate?.ToUniversalTime(),
                state.ToDate?.ToUniversalTime(),
                model.Access_token,
                state.Type);

            return ExportToExternalStorage();
        }

        private string GetPasswordFromSettings()
        {
            return this.exportSettings.EncryptionEnforced()
                ? this.exportSettings.GetPassword()
                : null;
        }

        public class ExportToExternalStorageModel
        {
            public string Access_token { get; set; }
            public string State { get; set; }
        }

        public class ExternalStorageStateModel
        {
            public ExternalStorageType Type { get; set; }
            public QuestionnaireIdentity QuestionnaireIdentity { get; set; }
            public InterviewStatus? InterviewStatus { get; set; }
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public DataExportFormat? Format { get; set; }
        }
    }
}