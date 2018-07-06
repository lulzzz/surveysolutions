﻿using System.Collections.Generic;
using System.Linq;
using Ncqrs.Eventing;
using NUnit.Framework;
using WB.Core.SharedKernels.DataCollection.Events.Interview;
using WB.Core.SharedKernels.Enumerator.OfflineSync.Messages;
using WB.Core.SharedKernels.Enumerator.OfflineSync.Services.Implementation;
using WB.Tests.Abc;

namespace WB.Tests.Unit.SharedKernels.Enumerator.OfflineSyncTests
{
    [TestOf(typeof(PostInterviewRequest))]
    public class PostInterviewRequestTests
    {
        [Test]
        public void should_be_able_to_serialize_and_deserialize_events()
        {
            var serializer = new PayloadSerializer();
            InterviewCreated interviewCreated = Create.Event.InterviewCreated();

            PostInterviewRequest request = new PostInterviewRequest(Id.gA, new List<CommittedEvent>
            {
                Create.Other.CommittedEvent(payload: interviewCreated, eventSourceId: Id.gA)
            });
            // Act
            byte[] payload = serializer.ToPayload(request);
            Assert.That(payload, Is.Not.Empty);

            var deserialized = serializer.FromPayload<PostInterviewRequest>(payload);

            Assert.That(deserialized.InterviewId, Is.EqualTo(Id.gA));
            Assert.That(deserialized.Events, Has.Count.EqualTo(1));
            Assert.That(deserialized.Events.First().Payload, Is.TypeOf<InterviewCreated>());
        }
    }
}