﻿using System;
using System.Linq;
using Ncqrs.Eventing.ServiceModel.Bus;
using WB.Core.Infrastructure.EventHandlers;
using WB.Core.Infrastructure.ReadSide.Repository.Accessors;
using WB.Core.SharedKernels.DataCollection.Events.Assignment;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.Core.SharedKernels.DataCollection.Repositories;

namespace WB.Core.BoundedContexts.Headquarters.Assignments
{
    public class AssignmentDenormalizer : AbstractFunctionalEventHandlerOnGuid<Assignment, IReadSideRepositoryWriter<Assignment, Guid>>,
        IUpdateHandler<Assignment, AssignmentCreated>,
        IUpdateHandler<Assignment, AssignmentDeleted>,
        IUpdateHandler<Assignment, AssignmentArchived>,
        IUpdateHandler<Assignment, AssignmentUnarchived>,
        IUpdateHandler<Assignment, AssignmentReassigned>,
        IUpdateHandler<Assignment, AssignmentWebModeChanged>,
        IUpdateHandler<Assignment, AssignmentAudioRecordingChanged>,
        IUpdateHandler<Assignment, AssignmentQuantityChanged>,
        IUpdateHandler<Assignment, AssignmentReceivedByTablet>
    {
        private readonly IQuestionnaireStorage questionnaireStorage;

        public AssignmentDenormalizer(IReadSideRepositoryWriter<Assignment, Guid> readSideStorage,
            IQuestionnaireStorage questionnaireStorage)
            : base(readSideStorage)
        {
            this.questionnaireStorage = questionnaireStorage;
        }


        public Assignment Update(Assignment state, IPublishedEvent<AssignmentCreated> @event)
        {
            state = new Assignment(
                @event.EventSourceId,
                @event.Payload.Id,
                new QuestionnaireIdentity(@event.Payload.QuestionnaireId, @event.Payload.QuestionnaireVersion), 
                @event.Payload.ResponsibleId,
                @event.Payload.Quantity,
                @event.Payload.AudioRecording,
                @event.Payload.Email,
                @event.Payload.Password,
                @event.Payload.WebMode,
                @event.Payload.Comment);

            var questionnaire = questionnaireStorage.GetQuestionnaire(state.QuestionnaireId, null);
            var identifyingQuestionIds = questionnaire.GetPrefilledQuestions().ToHashSet();

            var identifyingAnswers = @event.Payload.Answers
                .Where(x => identifyingQuestionIds.Contains(x.Identity.Id))
                .Select(a => IdentifyingAnswer.Create(state, questionnaire, a.Answer.ToString(), a.Identity))
                .ToList();

            state.IdentifyingData = identifyingAnswers;
            state.Answers = @event.Payload.Answers;
            state.ProtectedVariables = @event.Payload.ProtectedVariables.ToList();

            state.CreatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;

            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentArchived> @event)
        {
            state.Archived = true;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentUnarchived> @event)
        {
            state.Archived = false;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentReassigned> @event)
        {
            state.ResponsibleId = @event.Payload.ResponsibleId;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            state.ReceivedByTabletAtUtc = null;

            if (!string.IsNullOrEmpty(@event.Payload.Comment))
                state.Comments = @event.Payload.Comment;

            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentWebModeChanged> @event)
        {
            state.WebMode = @event.Payload.WebMode;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentReceivedByTablet> @event)
        {
            state.ReceivedByTabletAtUtc = @event.Payload.OriginDate.UtcDateTime;
            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentAudioRecordingChanged> @event)
        {
            state.IsAudioRecordingEnabled = @event.Payload.AudioRecording;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentQuantityChanged> @event)
        {
            state.Quantity = @event.Payload.Quantity;
            state.UpdatedAtUtc = @event.Payload.OriginDate.UtcDateTime;
            return state;
        }

        public Assignment Update(Assignment state, IPublishedEvent<AssignmentDeleted> @event)
        {
            return null;
        }
    }
}