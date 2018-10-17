﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Refit;
using WB.Services.Export.CsvExport.Exporters;
using WB.Services.Export.ExportProcessHandlers.Implementation;
using WB.Services.Export.Interview;
using WB.Services.Export.Interview.Entities;
using WB.Services.Export.Questionnaire;

namespace WB.Services.Export.Services
{
    /// <summary>
    /// Should not be injected
    /// </summary>
    
    public interface IHeadquartersApi
    {
        [Get("/api/export/v1/interview?questionnaireIdentity={questionnaireIdentity}")]
        Task<List<InterviewToExport>> GetInterviewsToExportAsync([Refit.AliasAs("questionnaireIdentity")]QuestionnaireId questionnaireIdentity,
            InterviewStatus? status,
            DateTime? fromDate,
            DateTime? toDate);

        [Get("/api/export/v1/questionnaire/{id}")]
        Task<string> GetQuestionnaireAsync([AliasAs("id")] QuestionnaireId questionnaireId);

        [Get("/api/export/v1/interview/batch/diagnosticsInfo")]
        Task<InterviewDiagnosticsInfo[]> GetInterviewDiagnosticsInfoBatchAsync(
            [Query(CollectionFormat.Multi), AliasAs("id")] Guid[] interviewIds);

        [Get("/api/export/v1/interview/batch/commentaries")]
        Task<List<InterviewComment>> GetInterviewCommentsBatchAsync([Query(CollectionFormat.Multi), AliasAs("id")] Guid[] interviewIds);

        [Get("/api/export/v1/interview/batch/summaries")]
        Task<List<InterviewSummary>> GetInterviewSummariesBatchAsync([Query(CollectionFormat.Multi), AliasAs("id")] Guid[] interviewIds);

        [Get("/api/export/v1/interview/{id}")]
        Task<List<InterviewEntity>> GetInterviewAsync([Query] Guid id, [Query(CollectionFormat.Multi)] Guid[] entityId = null);

        [Get("/api/export/v1/interviews")]
        Task<List<InterviewEntity>> GetInterviewBatchAsync(
            [Query(CollectionFormat.Multi), AliasAs("id")]
            Guid[] interviewId, 
            [Query(CollectionFormat.Multi)]
            Guid[] entityId = null);

        [Get("/api/export/v1/interview/{interviewId}/image/{image}")]
        Task<HttpContent> GetInterviewImageAsync(Guid interviewId, string image);
        
        [Get("/api/export/v1/interview/{interviewId}/audio/{image}")]
        Task<HttpContent> GetInterviewAudioAsync(Guid interviewId, string image);

        [Get("/api/export/v1/interview/batch/history")]
        Task<List<InterviewHistoryView>> GetInterviewsHistory([Query(CollectionFormat.Multi)] Guid[] id);
    }
}