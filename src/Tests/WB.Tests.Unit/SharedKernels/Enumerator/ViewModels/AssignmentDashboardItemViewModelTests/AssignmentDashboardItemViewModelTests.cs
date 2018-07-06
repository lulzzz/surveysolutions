﻿using Moq;
using NUnit.Framework;
using WB.Core.GenericSubdomains.Portable.ServiceLocation;
using WB.Core.SharedKernels.Enumerator.ViewModels.Dashboard;
using WB.Tests.Abc;

namespace WB.Tests.Unit.SharedKernels.Enumerator.ViewModels.AssignmentDashboardItemViewModelTests
{
    [TestOf(typeof(AssignmentDashboardItemViewModel))]
    public class AssignmentDashboardItemViewModelTests
    {
        [Test]
        [SetUICulture("en-US")]
        public void when_initialized_should_build_titles()
        {
            var viewModel = CreateViewModel();

            // Act 
            viewModel.Init(Create.Entity.AssignmentDocument(12, quantity: 3, interviewsCount: 1,
                questionnaireIdentity: Create.Entity.QuestionnaireIdentity().ToString())
                .WithTitle("Questionnaire title")
                .Build());

            // Assrt
            Assert.That(viewModel, Has.Property(nameof(viewModel.Title)).EqualTo("Questionnaire title (v7)"));
            Assert.That(viewModel, Has.Property(nameof(viewModel.IdLabel)).EqualTo("#12"));
            Assert.That(viewModel, Has.Property(nameof(viewModel.SubTitle)).EqualTo("To collect: 3 interview(s)"));
        }

        AssignmentDashboardItemViewModel CreateViewModel()
        {
            return new Mock<AssignmentDashboardItemViewModel>(Mock.Of<IServiceLocator>()).Object;
        }
    }
}