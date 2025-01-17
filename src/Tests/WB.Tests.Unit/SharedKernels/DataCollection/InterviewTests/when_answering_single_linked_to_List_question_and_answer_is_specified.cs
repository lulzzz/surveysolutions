using System;
using FluentAssertions;
using FluentAssertions.Extensions;
using Main.Core.Entities.Composite;
using Ncqrs.Spec;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.DataCollection.Events.Interview;
using WB.Core.SharedKernels.DataCollection.Implementation.Aggregates;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Tests.Abc;


namespace WB.Tests.Unit.SharedKernels.DataCollection.InterviewTests
{
    internal class when_answering_single_linked_to_List_question_and_answer_is_specified : InterviewTestsContext
    {
        [NUnit.Framework.OneTimeSetUp] public void context () {
            var questionnaireId = Guid.Parse("DDDDDDDDDDDDDDDDDDDDDD0000000000");

            var questionnaire = Create.Entity.PlainQuestionnaire(Create.Entity.QuestionnaireDocumentWithOneChapter(children: new IComposite[]
            {
                Create.Entity.TextListQuestion(questionId: questionId),
                Create.Entity.SingleOptionQuestion(linkedQuestionId, linkedToQuestionId: questionId),
            }));

            IQuestionnaireStorage questionnaireRepository = CreateQuestionnaireRepositoryStubWithOneQuestionnaire(questionnaireId, questionnaire);

            interview = CreateInterview(questionnaireId: questionnaireId, questionnaireRepository: questionnaireRepository);

            interview.AnswerTextListQuestion(userId, questionId, RosterVector.Empty, DateTime.UtcNow, new[] { new Tuple<decimal, string>(1, "one") });
            eventContext = new EventContext();
            BecauseOf();
        }

        public void BecauseOf() => interview.AnswerSingleOptionQuestion(userId: userId, questionId: linkedQuestionId, originDate: answerTime, rosterVector: propagationVector, selectedValue:1);

        [NUnit.Framework.OneTimeTearDown] public void CleanUp()
        {
            eventContext.Dispose();
            eventContext = null;
        }

        [NUnit.Framework.Test] public void should_raise_SingleOptionQuestionAnswered_event () =>
            eventContext.ShouldContainEvent<SingleOptionQuestionAnswered>();

        [NUnit.Framework.Test] public void should_raise_SingleOptionQuestionAnswered_event_with_QuestionId_equal_to_questionId () =>
            eventContext.GetSingleEvent<SingleOptionQuestionAnswered>().QuestionId.Should().Be(linkedQuestionId);

        [NUnit.Framework.Test] public void should_raise_SingleOptionQuestionAnswered_event_with_UserId_equal_to_userId () =>
            eventContext.GetSingleEvent<SingleOptionQuestionAnswered>().UserId.Should().Be(userId);

        [NUnit.Framework.Test] public void should_raise_SingleOptionQuestionAnswered_event_with_PropagationVector_equal_to_propagationVector () =>
            eventContext.GetSingleEvent<SingleOptionQuestionAnswered>().RosterVector.Should().BeEquivalentTo(propagationVector);

        [NUnit.Framework.Test] public void should_raise_SingleOptionQuestionAnswered_event_with_AnswerTime_equal_to_answerTime () =>
            eventContext.GetSingleEvent<SingleOptionQuestionAnswered>().AnswerTimeUtc.Should().Be(answerTime.UtcDateTime);

        private static EventContext eventContext;
        private static Interview interview;
        private static Guid userId = Guid.Parse("FFFFFFFFFFFFFFFFFFFFFF1111111111");
        private static Guid questionId = Guid.Parse("11111111111111111111111111111111");
        private static Guid linkedQuestionId = Guid.Parse("A1111111111111111111111111111111");
        private static readonly decimal[] propagationVector = RosterVector.Empty;
        private static DateTimeOffset answerTime = new DateTimeOffset(2.August(2010).At(22, 00));
    }
}
