﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Main.Core.Documents;
using Main.Core.Entities.Composite;
using Main.Core.Entities.SubEntities;
using Moq;
using NUnit.Framework;
using WB.Core.BoundedContexts.Headquarters.AssignmentImport;
using WB.Core.BoundedContexts.Headquarters.AssignmentImport.Parser;
using WB.Core.BoundedContexts.Headquarters.AssignmentImport.Verifier;
using WB.Core.BoundedContexts.Headquarters.DataExport.Denormalizers;
using WB.Core.BoundedContexts.Headquarters.Factories;
using WB.Core.BoundedContexts.Headquarters.Implementation.Services;
using WB.Core.BoundedContexts.Headquarters.Implementation.Services.Export;
using WB.Core.BoundedContexts.Headquarters.Repositories;
using WB.Core.BoundedContexts.Headquarters.Services.Preloading;
using WB.Core.BoundedContexts.Headquarters.Views.DataExport;
using WB.Core.BoundedContexts.Headquarters.Views.User;
using WB.Core.GenericSubdomains.Portable;
using WB.Core.Infrastructure.FileSystem;
using WB.Core.GenericSubdomains.Portable.Implementation.ServiceVariables;
using WB.Core.SharedKernels.DataCollection.Implementation.Entities;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Core.SharedKernels.DataCollection.ValueObjects;
using WB.Core.SharedKernels.DataCollection.Views.Questionnaire;
using WB.Core.BoundedContexts.Headquarters.Services;
using WB.Core.BoundedContexts.Headquarters.Views.Questionnaire;
using WB.Core.Infrastructure.PlainStorage;
using WB.Tests.Abc;

namespace WB.Tests.Unit.SharedKernels.SurveyManagement.PreloadedDataVerifierTests
{
    [TestOf(typeof(ImportDataVerifier))]
    internal class PreloadedDataVerifierTestContext
    {
        protected static ImportDataVerifier CreatePreloadedDataVerifier(
            QuestionnaireDocument questionnaireDocument = null, 
            IPreloadedDataService preloadedDataService = null,
            IUserViewFactory userViewFactory=null)
        {
            status = Create.Entity.AssignmentImportStatus();

            var questionnaire = questionnaireDocument == null
                ? null
                : Create.Entity.PlainQuestionnaire(questionnaireDocument, 1, null);
            

            var questionnaireExportStructure = questionnaireDocument == null
                ? null
                : new ExportViewFactory(Mock.Of<IFileSystemAccessor>(), 
                        new ExportQuestionService(),
                        Mock.Of<IQuestionnaireStorage>(x => x.GetQuestionnaire(Moq.It.IsAny<QuestionnaireIdentity>(), Moq.It.IsAny<string>()) == Create.Entity.PlainQuestionnaire(questionnaireDocument, 1, null) && 
                                                            x.GetQuestionnaireDocument(Moq.It.IsAny<QuestionnaireIdentity>()) == questionnaireDocument),
                        new RosterStructureService(),
                        Mock.Of<IPlainStorageAccessor<QuestionnaireBrowseItem>>())
                    .CreateQuestionnaireExportStructure(new QuestionnaireIdentity());

            var questionnaireRosterStructure = questionnaireDocument == null
                ? null
                : new RosterStructureService().GetRosterScopes(questionnaireDocument);

            var preloadedService = new ImportDataParsingService(questionnaireExportStructure, questionnaireRosterStructure,
                questionnaireDocument, new QuestionDataParser(), Mock.Of<IUserViewFactory>());
            return
                new ImportDataVerifier(
                    Mock.Of<IPreloadedDataServiceFactory>(
                        _ =>
                            _.CreatePreloadedDataService(Moq.It.IsAny<QuestionnaireExportStructure>(),
                                Moq.It.IsAny<Dictionary<ValueVector<Guid>, RosterScopeDescription>>(), Moq.It.IsAny<QuestionnaireDocument>()) ==
                            (preloadedDataService ?? preloadedService)),
                    userViewFactory ?? Mock.Of<IUserViewFactory>(),
                    Mock.Of<IQuestionnaireStorage>( _ => _.GetQuestionnaire(Moq.It.IsAny<QuestionnaireIdentity>(), Moq.It.IsAny<string>()) == questionnaire && 
                                                         _.GetQuestionnaireDocument(Moq.It.IsAny<Guid>(), Moq.It.IsAny<long>()) == questionnaireDocument),
                    Mock.Of<IQuestionnaireExportStructureStorage>(_ => _.GetQuestionnaireExportStructure(Moq.It.IsAny<QuestionnaireIdentity>()) == questionnaireExportStructure),
                    Mock.Of<IRosterStructureService>(_ => _.GetRosterScopes(Moq.It.IsAny<QuestionnaireDocument>()) == questionnaireRosterStructure));
        }

        protected static PreloadedDataByFile CreatePreloadedDataByFile(string[] header=null, string[][] content=null, string fileName=null)
        {
            return new PreloadedDataByFile(Guid.NewGuid().FormatGuid(), fileName ?? "some file", header ?? new string[] { ServiceColumns.InterviewId, ServiceColumns.ParentId },
                content ?? new string[0][]);
        }

        protected static QuestionnaireDocument CreateQuestionnaireDocumentWithOneChapter(params IComposite[] chapterChildren)
        {
            return new QuestionnaireDocument
            {
                Title = "some title",
                Children = new List<IComposite>
                {
                    new Group("Chapter")
                    {
                        PublicKey = Guid.Parse("FFF000AAA111EE2DD2EE111AAA000FFF"),
                        Children = chapterChildren?.ToReadOnlyCollection() ?? new ReadOnlyCollection<IComposite>(new List<IComposite>()),
                        IsRoster = false
                    }
                }.ToReadOnlyCollection()
            };
        }

        protected static AssignmentImportStatus status = Create.Entity.AssignmentImportStatus();
    }
}