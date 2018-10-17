using System;
using System.Linq;
using FluentAssertions;
using Main.Core.Documents;
using Main.Core.Entities.Composite;
using Main.Core.Entities.SubEntities;
using Main.Core.Entities.SubEntities.Question;
using Moq;
using WB.Core.BoundedContexts.Headquarters.DataExport.Denormalizers;
using WB.Core.BoundedContexts.Headquarters.Views.DataExport;
using WB.Core.BoundedContexts.Headquarters.Views.Interview;
using WB.Core.SharedKernels.DataCollection.Aggregates;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Core.SharedKernels.DataCollection.ValueObjects;
using WB.Tests.Abc;


namespace WB.Tests.Unit.SharedKernels.SurveyManagement.Factories.ExportViewFactoryTests
{
    internal class when_creating_interview_export_view_by_interview_with_linked_question_on_second_level_referenced_on_third : ExportViewFactoryTestsContext
    {
        [NUnit.Framework.OneTimeSetUp] public void context () {
            linkedQuestionSourceId = Guid.Parse("12222222222222222222222222222222");
            rosterId = Guid.Parse("13333333333333333333333333333333");
            var nestedRosterId = Guid.Parse("23333333333333333333333333333333");

            linkedQuestionId = Guid.Parse("10000000000000000000000000000000");

            questionnaireDocument = CreateQuestionnaireDocumentWithOneChapter(
                Create.Entity.FixedRoster(rosterId: rosterId, obsoleteFixedTitles: new[] {"t1", "t2"},
                    children: new IComposite[]
                    {
                        new SingleQuestion()
                        {
                            PublicKey = linkedQuestionId,
                            QuestionType = QuestionType.SingleOption,
                            LinkedToQuestionId = linkedQuestionSourceId
                        },
                        Create.Entity.FixedRoster(rosterId: nestedRosterId, obsoleteFixedTitles: new[] {"n1", "n2"},
                            children: new IComposite[]
                            {
                                new NumericQuestion()
                                {
                                    PublicKey = linkedQuestionSourceId,
                                    QuestionType = QuestionType.Numeric,
                                    StataExportCaption = "q1"
                                }
                            })
                    }));

            interview = CreateInterviewData();
            var rosterLevel = new InterviewLevel(new ValueVector<Guid> { rosterId }, null, new decimal[] { 0 });
            interview.Levels.Add("0", rosterLevel);

            if (!rosterLevel.QuestionsSearchCache.ContainsKey(linkedQuestionId))
                rosterLevel.QuestionsSearchCache.Add(linkedQuestionId, new InterviewQuestion(linkedQuestionId));

            var textListQuestion = rosterLevel.QuestionsSearchCache[linkedQuestionId];

            textListQuestion.Answer = new decimal[] { 0, 0 };

            var questionnaireMockStorage = new Mock<IQuestionnaireStorage>();
            questionnaire = Create.Entity.PlainQuestionnaire(questionnaireDocument, 1, null);
            questionnaireMockStorage.Setup(x => x.GetQuestionnaire(Moq.It.IsAny<QuestionnaireIdentity>(), Moq.It.IsAny<string>())).Returns(questionnaire);
            questionnaireMockStorage.Setup(x => x.GetQuestionnaireDocument(Moq.It.IsAny<QuestionnaireIdentity>())).Returns(questionnaireDocument);
            exportViewFactory = CreateExportViewFactory(questionnaireMockStorage.Object);
            BecauseOf();
        }

        public void BecauseOf() =>
             result = exportViewFactory.CreateInterviewDataExportView(exportViewFactory.CreateQuestionnaireExportStructure(new QuestionnaireIdentity(questionnaireDocument.PublicKey, 1)),
                interview, questionnaire);

        [NUnit.Framework.Test] public void should_linked_question_have_one_answer () =>
           GetLevel(result, new[] { rosterId }).Records[0].GetPlainAnswers().First().Length.Should().Be(1);

        [NUnit.Framework.Test] public void should_linked_question_have_first_answer_be_equal_to_0 () =>
           GetLevel(result, new[] { rosterId }).Records[0].GetPlainAnswers().First().First().Should().Be("0");

      
        private static InterviewDataExportView result;
        private static Guid rosterId;
        private static Guid linkedQuestionId;
        private static Guid linkedQuestionSourceId;
        private static QuestionnaireDocument questionnaireDocument;
        private static IQuestionnaire questionnaire;
        private static ExportViewFactory exportViewFactory;
        private static InterviewData interview;
    }
}