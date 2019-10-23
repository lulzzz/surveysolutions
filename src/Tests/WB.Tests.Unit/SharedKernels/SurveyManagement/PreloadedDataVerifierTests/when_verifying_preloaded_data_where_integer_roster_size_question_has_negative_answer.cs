using System;
using System.Linq;
using FluentAssertions;
using Main.Core.Documents;
using Main.Core.Entities.Composite;
using Main.Core.Entities.SubEntities;
using WB.Core.BoundedContexts.Headquarters.AssignmentImport.Parser;
using WB.Core.BoundedContexts.Headquarters.AssignmentImport.Verifier;
using WB.Core.BoundedContexts.Headquarters.ValueObjects.PreloadedData;
using WB.Core.GenericSubdomains.Portable.Implementation.ServiceVariables;
using WB.Tests.Abc;

namespace WB.Tests.Unit.SharedKernels.SurveyManagement.PreloadedDataVerifierTests
{
    internal class when_verifying_preloaded_data_where_integer_roster_size_question_has_negative_answer : PreloadedDataVerifierTestContext
    {
        [NUnit.Framework.OneTimeSetUp] public void context () {
            questionnaireId = Guid.Parse("11111111111111111111111111111111");
            numericQuestionId = Guid.Parse("21111111111111111111111111111111");
            var numericQuestion = Create.Entity.NumericIntegerQuestion(numericQuestionId, "num");

            questionnaire =
                CreateQuestionnaireDocumentWithOneChapter(chapterChildren:
                    new IComposite[]
                    {
                        numericQuestion,
                        Create.Entity.Roster(rosterSizeQuestionId: numericQuestionId,
                            rosterSizeSourceType: RosterSizeSourceType.Question)
                    });

            questionnaire.Title = "questionnaire";
            preloadedDataByFile = CreatePreloadedDataByFile(new[] { ServiceColumns.InterviewId, "num" },
                new string[][] { new string[] { "1", "-3" } },
                "questionnaire.csv");

            var preloadedDataService =
                Create.Service.PreloadedDataService(questionnaire);

            importDataVerifier = CreatePreloadedDataVerifier(questionnaire, preloadedDataService);

            BecauseOf();
        }

        private void BecauseOf() => importDataVerifier.VerifyPanelFiles(questionnaireId, 1, Create.Entity.PreloadedDataByFile(preloadedDataByFile), status);

        [NUnit.Framework.Test] public void should_result_has_1_error () =>
            status.VerificationState.Errors.Count().Should().Be(1);

        [NUnit.Framework.Test] public void should_return_single_PL0022_error () =>
            status.VerificationState.Errors.First().Code.Should().Be("PL0022");

        [NUnit.Framework.Test] public void should_return_reference_with_Cell_type () =>
            status.VerificationState.Errors.First().References.First().Type.Should().Be(PreloadedDataVerificationReferenceType.Cell);

        private static ImportDataVerifier importDataVerifier;
        private static QuestionnaireDocument questionnaire;
        private static Guid questionnaireId;
        private static Guid numericQuestionId;
        private static PreloadedDataByFile preloadedDataByFile;
    }
}