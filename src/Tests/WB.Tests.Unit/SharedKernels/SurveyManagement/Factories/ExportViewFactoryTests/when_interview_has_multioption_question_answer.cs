using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Main.Core.Entities.SubEntities;
using Moq;
using WB.Core.BoundedContexts.Headquarters.DataExport.Denormalizers;
using WB.Core.BoundedContexts.Headquarters.Views.DataExport;
using WB.Core.BoundedContexts.Headquarters.Views.Interview;
using WB.Core.SharedKernels.DataCollection.Aggregates;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Tests.Abc;


namespace WB.Tests.Unit.SharedKernels.SurveyManagement.Factories.ExportViewFactoryTests
{
    internal class when_interview_has_multioption_question_answer : ExportViewFactoryTestsContext
    {
        [NUnit.Framework.OneTimeSetUp] public void context () {
            questionId = Guid.Parse("d7127d06-5668-4fa3-b255-8a2a0aaaa020");

            var questionnaireDocument = Create.Entity.QuestionnaireDocumentWithOneChapter(
                Create.Entity.MultyOptionsQuestion(id: questionId, options: new List<Answer> {Create.Entity.Answer("foo", 28), Create.Entity.Answer("bar", 42), Create.Entity.Answer("bartender", 18) }));

            var questionnaireMockStorage = new Mock<IQuestionnaireStorage>();
            questionnaire = Create.Entity.PlainQuestionnaire(questionnaireDocument, 1, null);
            questionnaireMockStorage.Setup(x => x.GetQuestionnaire(Moq.It.IsAny<QuestionnaireIdentity>(), Moq.It.IsAny<string>())).Returns(questionnaire);
            questionnaireMockStorage.Setup(x => x.GetQuestionnaireDocument(Moq.It.IsAny<QuestionnaireIdentity>())).Returns(questionnaireDocument);
            exportViewFactory = CreateExportViewFactory(questionnaireMockStorage.Object);

            questionnaaireExportStructure = exportViewFactory.CreateQuestionnaireExportStructure(questionnaireDocument.PublicKey, 1);

            interview = Create.Entity.InterviewData(Create.Entity.InterviewQuestion(questionId, new [] {42, 18}));
            BecauseOf();
        }

         public void BecauseOf() => result = exportViewFactory.CreateInterviewDataExportView(questionnaaireExportStructure, interview, questionnaire);

        [NUnit.Framework.Test] public void should_put_answers_to_export () => result.Levels.Length.Should().Be(1);

        [NUnit.Framework.Test] public void should_put_answers_to_export_in_appropriate_order () 
        {
            InterviewDataExportLevelView first = result.Levels.First();
            var exportedQuestion = first.Records.First().GetPlainAnswers().First();
            exportedQuestion.Length.Should().Be(3);
            exportedQuestion.Should().BeEquivalentTo(new [] {"0", "1", "1"});
        }

        static ExportViewFactory exportViewFactory;
        static QuestionnaireExportStructure questionnaaireExportStructure;
        static InterviewDataExportView result;
        static InterviewData interview;
        static Guid questionId;
        static IQuestionnaire questionnaire;
    }
}