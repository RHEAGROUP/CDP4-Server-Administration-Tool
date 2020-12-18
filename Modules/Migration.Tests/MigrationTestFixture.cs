﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MigrationTestFixture.cs">
//    Copyright (c) 2020
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Migration.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CDP4Common.CommonData;
    using CDP4Common.EngineeringModelData;
    using CDP4Dal.Operations;
    using CDP4Common.SiteDirectoryData;
    using CDP4Common.Types;
    using CDP4Dal;
    using CDP4Dal.DAL;
    using Common.Settings;
    using Common.ViewModels;
    using Common.ViewModels.PlainObjects;
    using Views;
    using Moq;
    using NUnit.Framework;
    using ViewModels;

    /// <summary>
    /// Suite of tests for the <see cref="MigrationViewModel"/>
    /// </summary>
    [TestFixture]
    public class MigrationTestFixture
    {
        private readonly Credentials credentials = new Credentials(
            "John",
            "Doe",
            new Uri("http://www.rheagroup.com/"));

        private Mock<ISession> session;
        private Mock<IDal> dal;
        private Mock<IDal> jsonFileDal;
        private Mock<ILoginViewModel> sourceViewModel;
        private Mock<ILoginViewModel> targetViewModel;
        private Assembler assembler;
        private MigrationViewModel migrationViewModel;
        private Mock<IFixCoordinalityErrorsDialog> viewDialog;

        private EngineeringModelSetup engineeringModelSetup;
        private SiteDirectory siteDirectory;
        private Dictionary<Guid, CDP4Common.DTO.Thing> sessionThings;
        private Iteration iteration;
        private DomainOfExpertise domain;
        private Person person;

        [SetUp]
        public void SetUp()
        {
            //RxApp.MainThreadScheduler = Scheduler.CurrentThread;

            AppSettingsHandler.Settings = new AppSettings
            {
                SavedUris = new List<string>()
            };

            this.dal = new Mock<IDal>();
            this.jsonFileDal = new Mock<IDal>();
            this.jsonFileDal.Setup(x => x.DalVersion).Returns(new Version("1.0.0"));
            this.dal.SetupProperty(d => d.Session);
            this.assembler = new Assembler(this.credentials.Uri);
            this.viewDialog = new Mock<IFixCoordinalityErrorsDialog>();
            this.viewDialog.Setup(x => x.ShowDialog()).Returns(true);

            this.session = new Mock<ISession>();
            this.session.Setup(x => x.Dal).Returns(this.dal.Object);
            this.session.Setup(x => x.DalVersion).Returns(new Version(1, 1, 0));
            this.session.Setup(x => x.Credentials).Returns(this.credentials);
            this.session.Setup(x => x.Assembler).Returns(this.assembler);

            this.InitDalOperations();
            this.InitSessionThings();

            this.session.Setup(x => x.RetrieveSiteDirectory()).Returns(this.siteDirectory);

            this.migrationViewModel = new MigrationViewModel();
            this.migrationViewModel.AddSubscriptions();

            this.sourceViewModel = new Mock<ILoginViewModel>();
            this.sourceViewModel.Setup(x => x.SelectedDataSource).Returns(DataSource.CDP4);
            this.sourceViewModel.Setup(x => x.UserName).Returns(this.credentials.UserName);
            this.sourceViewModel.Setup(x => x.Password).Returns(this.credentials.Password);
            this.sourceViewModel.Setup(x => x.Uri).Returns(this.credentials.Uri.ToString());
            this.sourceViewModel.Setup(x => x.Dal).Returns(this.dal.Object);
            this.sourceViewModel.Setup(x => x.ServerSession).Returns(this.session.Object);

            this.targetViewModel = new Mock<ILoginViewModel>();
            this.targetViewModel.Setup(x => x.SelectedDataSource).Returns(DataSource.CDP4);
            this.targetViewModel.Setup(x => x.UserName).Returns(this.credentials.UserName);
            this.targetViewModel.Setup(x => x.Password).Returns(this.credentials.Password);
            this.targetViewModel.Setup(x => x.Uri).Returns(this.credentials.Uri.ToString());
            this.targetViewModel.Setup(x => x.Dal).Returns(this.dal.Object);
            this.targetViewModel.Setup(x => x.ServerSession).Returns(this.session.Object);
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test, Apartment(ApartmentState.STA)]
        public void VerifyIfMigrationStartWithSourceAndTargetSessionSet()
        {
            this.assembler.Cache.TryAdd(new CacheKey(this.iteration.Iid, null), new Lazy<Thing>(() => this.iteration));
            this.session.Setup(x => x.ActivePerson).Returns(this.person);
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain, null)}
                });

            this.sourceViewModel.Setup(x => x.LoginSuccessfully).Returns(true);
            this.migrationViewModel.SourceViewModel = this.sourceViewModel.Object;

            this.targetViewModel.Setup(x => x.LoginSuccessfully).Returns(true);
            this.migrationViewModel.TargetViewModel = this.targetViewModel.Object;

            this.migrationViewModel.MigrationFactory.Dal = this.jsonFileDal.Object;

            Assert.IsTrue(this.migrationViewModel.CanMigrate);

            var selectedEngineeringModels = new List<EngineeringModelRowViewModel>
            {
                new EngineeringModelRowViewModel(this.engineeringModelSetup)
            };
            var firstSelected = selectedEngineeringModels.FirstOrDefault();
            if (firstSelected != null) firstSelected.IsSelected = true;

            this.sourceViewModel.Setup(x => x.EngineeringModels).Returns(selectedEngineeringModels);

            this.migrationViewModel.FixDialog = this.viewDialog.Object;

            Assert.DoesNotThrowAsync(async () =>
                await this.migrationViewModel.MigrateCommand.ExecuteAsyncTask());
        }

        [Test]
        public void VerifyIfMigrationNotStartWithoutSourceAndTargetSessionSet()
        {
            Assert.IsNull(this.migrationViewModel.SourceViewModel);
            Assert.IsNull(this.migrationViewModel.TargetViewModel);
        }

        private void InitSessionThings()
        {
            // Site Directory
            this.siteDirectory = new SiteDirectory(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri);

            this.domain = new DomainOfExpertise(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
            {
                Name = "domain"

            };
            siteDirectory.Domain.Add(domain);

            this.person = new Person(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
            {
                ShortName = credentials.UserName,
                GivenName = credentials.UserName,
                Password = credentials.Password,
                DefaultDomain = domain,
                IsActive = true
            };
            siteDirectory.Person.Add(person);

            var participant = new Participant(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri) { Person = person };
            participant.Domain.Add(domain);


            // Site Rld
            var siteReferenceDataLibrary =
                new SiteReferenceDataLibrary(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
                {
                    ShortName = "Generic_RDL"
                };
            var quantityKindParamType = new SimpleQuantityKind(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
            {
                Name = "m",
                ShortName = "m"
            };
            siteReferenceDataLibrary.ParameterType.Add(quantityKindParamType);

            // Model Rdl
            var modelReferenceDataLibrary =
                new ModelReferenceDataLibrary(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
                {
                    RequiredRdl = siteReferenceDataLibrary
                };
            siteDirectory.SiteReferenceDataLibrary.Add(siteReferenceDataLibrary);

            // Iteration
            this.iteration = new Iteration(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri);
            var iterationSetup = new IterationSetup(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
            {
                IterationIid = iteration.Iid
            };

            // Engineering Model & Setup
            var engineeringModel = new EngineeringModel(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri);
            engineeringModel.Iteration.Add(iteration);

            this.engineeringModelSetup = new EngineeringModelSetup(Guid.NewGuid(), this.session.Object.Assembler.Cache, this.session.Object.Credentials.Uri)
            { EngineeringModelIid = engineeringModel.Iid };
            engineeringModel.EngineeringModelSetup = engineeringModelSetup;
            engineeringModelSetup.RequiredRdl.Add(modelReferenceDataLibrary);
            engineeringModelSetup.IterationSetup.Add(iterationSetup);
            engineeringModelSetup.Participant.Add(participant);
            siteDirectory.Model.Add(engineeringModelSetup);

            this.sessionThings = new Dictionary<Guid, CDP4Common.DTO.Thing>
            {
                {siteDirectory.Iid, siteDirectory.ToDto()},
                {domain.Iid, domain.ToDto()},
                {person.Iid, person.ToDto()},
                {participant.Iid, participant.ToDto()},
                {siteReferenceDataLibrary.Iid, siteReferenceDataLibrary.ToDto()},
                {quantityKindParamType.Iid, quantityKindParamType.ToDto()},
                {modelReferenceDataLibrary.Iid, modelReferenceDataLibrary.ToDto()},
                {engineeringModelSetup.Iid, engineeringModelSetup.ToDto()},
                {iteration.Iid, iteration.ToDto()},
                {iterationSetup.Iid, iterationSetup.ToDto()},
                {engineeringModel.Iid, engineeringModel.ToDto()}
            };
        }

        private void InitDalOperations()
        {
            this.dal.Setup(x => x.Open(this.credentials, It.IsAny<CancellationToken>()))
                .Returns<Credentials, CancellationToken>(
                    (dalCredentials, dalCancellationToken) =>
                    {
                        var result = this.sessionThings.Values.ToList() as IEnumerable<CDP4Common.DTO.Thing>;
                        return Task.FromResult(result);
                    });

            this.jsonFileDal.Setup(x => x.Write(It.IsAny<OperationContainer>(), It.IsAny<IEnumerable<string>>()))
                .Returns<OperationContainer, IEnumerable<string>>((operationContainer, files) =>
                    Task.FromResult((IEnumerable<CDP4Common.DTO.Thing>) new List<CDP4Common.DTO.Thing>()));
        }
    }
}
