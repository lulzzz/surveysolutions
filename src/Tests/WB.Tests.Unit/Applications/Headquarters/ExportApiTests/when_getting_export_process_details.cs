using System;
using System.Web.Http;
using System.Web.Http.Results;
using FluentAssertions;
using Moq;
using WB.Core.BoundedContexts.Headquarters.DataExport.Dtos;
using WB.Core.BoundedContexts.Headquarters.DataExport.Views;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.UI.Headquarters.API.PublicApi;

namespace WB.Tests.Unit.Applications.Headquarters.ExportApiTests
{
    public class when_getting_export_process_details : ExportControllerTestsContext
    {
        [NUnit.Framework.OneTimeSetUp] public void context () {
            var mockOfDataExportStatusReader = new Mock<IDataExportStatusReader>();
            dataExportStatusView = new DataExportStatusView(questionnaireIdentity.QuestionnaireId, questionnaireIdentity.Version,
                new[]
                {
                    new DataExportView
                    {
                        HasDataToExport = true,
                        DataExportFormat = DataExportFormat.Tabular,
                        LastUpdateDate = new DateTime(2016, 01, 16),
                        StatusOfLatestExportProcess = DataExportStatus.Running
                    }
                }, new[]
                {
                    new RunningDataExportProcessView
                    {
                        QuestionnaireIdentity = questionnaireIdentity,
                        BeginDate = new DateTime(2016, 01, 17, 12, 12, 12),
                        Type = DataExportType.Data,
                        Format = DataExportFormat.Tabular,
                        ProcessStatus = DataExportStatus.Running,
                        Progress = 98
                    },
                });
            mockOfDataExportStatusReader.Setup(x => x.GetDataExportStatusForQuestionnaire(questionnaireIdentity, null, null, null))
                .Returns(dataExportStatusView);

            controller = CreateExportController(dataExportStatusReader: mockOfDataExportStatusReader.Object);
            BecauseOf();
        }

        private void BecauseOf() => result = controller.ProcessDetails(questionnaireIdentity.ToString(), DataExportFormat.Tabular);

        [NUnit.Framework.Test] public void should_return_http_ok_response () =>
            result.Should().BeOfType<OkNegotiatedContentResult<ExportController.ExportDetails>>();

        [NUnit.Framework.Test] public void should_return_specified_json_object ()
        {
            var jsonResult = ((OkNegotiatedContentResult<ExportController.ExportDetails>) result).Content;

            jsonResult.ExportStatus.Should().Be(dataExportStatusView.DataExports[0].StatusOfLatestExportProcess);
            jsonResult.HasExportedFile.Should().Be(dataExportStatusView.DataExports[0].HasDataToExport);
            jsonResult.LastUpdateDate.Should().Be(dataExportStatusView.DataExports[0].LastUpdateDate);
            jsonResult.RunningProcess.ProgressInPercents.Should().Be(
                dataExportStatusView.RunningDataExportProcesses[0].Progress);
            jsonResult.RunningProcess.StartDate.Should().Be(dataExportStatusView.RunningDataExportProcesses[0].BeginDate);

        }
            

        private static ExportController controller;

        private static IHttpActionResult result;
        private static readonly QuestionnaireIdentity questionnaireIdentity = new QuestionnaireIdentity(Guid.Parse("11111111111111111111111111111111"), 1);
        private static DataExportStatusView dataExportStatusView;
    }
}