﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StressGeneratorTestFixture.cs" company="RHEA System S.A.">
//    Copyright (c) 2015-2021 RHEA System S.A.
// 
//    Author: Adrian Chivu, Cozmin Velciu, Alex Vorobiev
// 
//    This file is part of CDP4-Server-Administration-Tool.
//    The CDP4-Server-Administration-Tool is an ECSS-E-TM-10-25 Compliant tool
//    for advanced server administration.
// 
//    The CDP4-Server-Administration-Tool is free software; you can redistribute it and/or modify
//    it under the terms of the GNU Affero General Public License as
//    published by the Free Software Foundation; either version 3 of the
//    License, or (at your option) any later version.
// 
//    The CDP4-Server-Administration-Tool is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//    Affero General Public License for more details.
// 
//    You should have received a copy of the GNU Affero General Public License
//    along with this program. If not, see <http://www.gnu.org/licenses/>.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace StressGenerator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CDP4Common.DTO;
    using CDP4Common.EngineeringModelData;
    using CDP4Common.Types;
    using CDP4Dal;
    using CDP4Dal.DAL;
    using CDP4Dal.Operations;
    using Common.Settings;
    using Common.ViewModels;
    using Moq;
    using NUnit.Framework;
    using Utils;
    using ViewModels;
    using DomainOfExpertise = CDP4Common.SiteDirectoryData.DomainOfExpertise;
    using ElementDefinition = CDP4Common.EngineeringModelData.ElementDefinition;
    using EngineeringModel = CDP4Common.EngineeringModelData.EngineeringModel;
    using EngineeringModelSetup = CDP4Common.SiteDirectoryData.EngineeringModelSetup;
    using Iteration = CDP4Common.EngineeringModelData.Iteration;
    using IterationSetup = CDP4Common.SiteDirectoryData.IterationSetup;
    using ModelReferenceDataLibrary = CDP4Common.SiteDirectoryData.ModelReferenceDataLibrary;
    using Parameter = CDP4Common.EngineeringModelData.Parameter;
    using Participant = CDP4Common.SiteDirectoryData.Participant;
    using Person = CDP4Common.SiteDirectoryData.Person;
    using QuantityKind = CDP4Common.SiteDirectoryData.QuantityKind;
    using SimpleQuantityKind = CDP4Common.SiteDirectoryData.SimpleQuantityKind;
    using SiteDirectory = CDP4Common.SiteDirectoryData.SiteDirectory;
    using SiteReferenceDataLibrary = CDP4Common.SiteDirectoryData.SiteReferenceDataLibrary;

    /// <summary>
    /// Suite of tests for the <see cref="StressGeneratorViewModel" />
    /// </summary>
    [TestFixture]
    public class StressGeneratorTestFixture
    {
        [SetUp]
        public void Setup()
        {
            this.dal = new Mock<IDal>();
            this.dal.SetupProperty(d => d.Session);
            this.assembler = new Assembler(this.credentials.Uri);

            AppSettingsHandler.Settings = new AppSettings
            {
                SavedUris = new List<string>()
            };

            this.session = new Mock<ISession>();
            this.session.Setup(x => x.Dal).Returns(this.dal.Object);
            this.session.Setup(x => x.Credentials).Returns(this.credentials);
            this.session.Setup(x => x.Assembler).Returns(this.assembler);

            this.generatedThings = new Dictionary<Guid, Thing>();
            this.modifiedThings = new Dictionary<Guid, Thing>();

            this.InitSessionThings();

            this.InitDalOperations();
            this.session.Setup(s => s.Dal).Returns(this.dal.Object);

            this.InitViewModel();
        }

        private readonly Credentials credentials = new Credentials(
            "John",
            "Doe",
            new Uri("http://www.rheagroup.com/"));

        private Mock<ISession> session;
        private Mock<IDal> dal;
        private Assembler assembler;

        private Mock<ILoginViewModel> sourceViewModel;
        private StressGeneratorViewModel stressGeneratorViewModel;

        private SiteDirectory siteDirectory;
        private DomainOfExpertise domain;
        private Person person;
        private Participant participant;
        private QuantityKind quantityKindParamType;

        private SiteReferenceDataLibrary siteReferenceDataLibrary;
        private ModelReferenceDataLibrary modelReferenceDataLibrary;

        private EngineeringModel engineeringModel;
        private EngineeringModelSetup engineeringModelSetup;
        private Iteration iteration;
        private IterationSetup iterationSetup;

        private Dictionary<Guid, Thing> sessionThings;
        private Dictionary<Guid, Thing> generatedThings;
        private Dictionary<Guid, Thing> modifiedThings;

        private void InitSessionThings()
        {
            // Site Directory
            this.siteDirectory = new SiteDirectory(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri);

            this.domain = new DomainOfExpertise(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri)
            {
                Name = "domain"
            };
            this.siteDirectory.Domain.Add(this.domain);

            this.person = new Person(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri)
            {
                ShortName = this.credentials.UserName,
                GivenName = this.credentials.UserName,
                Password = this.credentials.Password,
                DefaultDomain = this.domain,
                IsActive = true
            };
            this.siteDirectory.Person.Add(this.person);

            this.participant =
                new Participant(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                    this.session.Object.Credentials.Uri) {Person = this.person};
            this.participant.Domain.Add(this.domain);

            // Site Rld
            this.siteReferenceDataLibrary =
                new SiteReferenceDataLibrary(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                    this.session.Object.Credentials.Uri)
                {
                    ShortName = "Generic_RDL"
                };
            this.quantityKindParamType = new SimpleQuantityKind(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri)
            {
                Name = "m",
                ShortName = "m"
            };
            this.siteReferenceDataLibrary.ParameterType.Add(this.quantityKindParamType);

            // Model Rdl
            this.modelReferenceDataLibrary =
                new ModelReferenceDataLibrary(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                    this.session.Object.Credentials.Uri)
                {
                    RequiredRdl = this.siteReferenceDataLibrary
                };
            this.siteDirectory.SiteReferenceDataLibrary.Add(this.siteReferenceDataLibrary);

            // Iteration
            this.iteration = new Iteration(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri);
            this.iterationSetup = new IterationSetup(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri)
            {
                IterationIid = this.iteration.Iid
            };

            // Engineering Model & Setup
            this.engineeringModel = new EngineeringModel(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                this.session.Object.Credentials.Uri)
            {
                EngineeringModelSetup = this.engineeringModelSetup
            };
            this.engineeringModel.Iteration.Add(this.iteration);

            var ed1 = new ElementDefinition(Guid.NewGuid(), null, null);
            var param1 = new Parameter(Guid.NewGuid(), null, null)
            {
                ParameterType = this.quantityKindParamType
            };

            ed1.Parameter.Add(param1);
            this.iteration.Element.Add(ed1);

            this.engineeringModelSetup = new EngineeringModelSetup(Guid.NewGuid(), this.session.Object.Assembler.Cache,
                    this.session.Object.Credentials.Uri)
                {EngineeringModelIid = this.engineeringModel.Iid};
            this.engineeringModelSetup.RequiredRdl.Add(this.modelReferenceDataLibrary);
            this.engineeringModelSetup.IterationSetup.Add(this.iterationSetup);
            this.engineeringModelSetup.Participant.Add(this.participant);
            this.siteDirectory.Model.Add(this.engineeringModelSetup);

            this.sessionThings = new Dictionary<Guid, Thing>
            {
                {this.siteDirectory.Iid, this.siteDirectory.ToDto()},
                {this.domain.Iid, this.domain.ToDto()},
                {this.person.Iid, this.person.ToDto()},
                {this.participant.Iid, this.participant.ToDto()},
                {this.siteReferenceDataLibrary.Iid, this.siteReferenceDataLibrary.ToDto()},
                {this.quantityKindParamType.Iid, this.quantityKindParamType.ToDto()},
                {this.modelReferenceDataLibrary.Iid, this.modelReferenceDataLibrary.ToDto()},
                {this.engineeringModelSetup.Iid, this.engineeringModelSetup.ToDto()},
                {this.iteration.Iid, this.iteration.ToDto()},
                {this.iterationSetup.Iid, this.iterationSetup.ToDto()},
                {this.engineeringModel.Iid, this.engineeringModel.ToDto()}
            };

            this.session = new Mock<ISession>();
            this.session.Setup(s => s.Assembler).Returns(this.assembler);
            this.session.Setup(s => s.Credentials).Returns(this.credentials);
            this.session.Setup(s => s.ActivePerson).Returns(this.person);
            this.session.Setup(s => s.RetrieveSiteDirectory()).Returns(this.siteDirectory);

            this.session.Setup(x => x.Write(It.IsAny<OperationContainer>(), It.IsAny<IEnumerable<string>>()))
                .Returns<OperationContainer, IEnumerable<string>>((operationContainer, files) =>
                {
                    foreach (var operation in operationContainer.Operations)
                    {
                        var operationThing = operation.ModifiedThing;

                        switch (operation.OperationKind)
                        {
                            case OperationKind.Create:
                                if (!this.generatedThings.ContainsKey(operationThing.Iid))
                                {
                                    this.generatedThings[operationThing.Iid] = operationThing;
                                }

                                break;
                            case OperationKind.Update:
                                if (!this.modifiedThings.ContainsKey(operationThing.Iid))
                                {
                                    this.modifiedThings[operationThing.Iid] = operationThing;
                                }

                                break;
                        }
                    }

                    return Task.FromResult((IEnumerable<Thing>)new List<Thing>());
                });
        }

        private void InitDalOperations()
        {
            this.dal.Setup(x => x.Open(this.credentials, It.IsAny<CancellationToken>()))
                .Returns<Credentials, CancellationToken>(
                    (dalCredentials, dalCancellationToken) =>
                    {
                        var result = this.sessionThings.Values.ToList() as IEnumerable<Thing>;
                        return Task.FromResult(result);
                    });

            this.dal.Setup(x => x.Write(It.IsAny<OperationContainer>(), It.IsAny<IEnumerable<string>>()))
                .Returns<OperationContainer, IEnumerable<string>>((operationContainer, files) =>
                {
                    foreach (var operation in operationContainer.Operations)
                    {
                        var operationThing = operation.ModifiedThing;

                        switch (operation.OperationKind)
                        {
                            case OperationKind.Create:
                                if (!this.generatedThings.ContainsKey(operationThing.Iid))
                                {
                                    this.generatedThings[operationThing.Iid] = operationThing;
                                }

                                break;
                            case OperationKind.Update:
                                if (!this.modifiedThings.ContainsKey(operationThing.Iid))
                                {
                                    this.modifiedThings[operationThing.Iid] = operationThing;
                                }

                                break;
                        }
                    }

                    return Task.FromResult((IEnumerable<Thing>) new List<Thing>());
                });
        }

        private void InitViewModel()
        {
            this.sourceViewModel = new Mock<ILoginViewModel>
            {
                Object =
                {
                    SelectedDataSource = DataSource.CDP4,
                    UserName = this.credentials.UserName,
                    Password = this.credentials.Password,
                    Uri = this.credentials.Uri.ToString(),
                    ServerSession = this.session.Object
                }
            };

            this.sourceViewModel.Setup(s => s.ServerSession).Returns(this.session.Object);

            this.stressGeneratorViewModel = new StressGeneratorViewModel
            {
                SourceViewModel = this.sourceViewModel.Object,
                TimeInterval = 1,
                TestObjectsNumber = 5,
                ElementName = "ElementDefinition",
                ElementShortName = "ED",
                SelectedEngineeringModelSetup = this.engineeringModelSetup
            };
        }

        [Test]
        public void VerifyThatStressGeneratorFailedIfSessionHasNoActivePerson()
        {
            Assert.DoesNotThrow(() => this.stressGeneratorViewModel.StressCommand.Execute(null));

            // No iteration will be modified
            Assert.AreEqual(0, this.modifiedThings.Count);

            // No element definition will be added
            Assert.AreEqual(0, this.generatedThings.Count);
        }

        [Test]
        public void VerifyThatStressGeneratorFailedIfSessionHasNoOpenIteration()
        {
            this.session.Setup(x => x.ActivePerson).Returns(this.person);

            Assert.DoesNotThrow(() => this.stressGeneratorViewModel.StressCommand.Execute(null));

            // No iteration will be modified
            Assert.AreEqual(0, this.modifiedThings.Count);

            // No element definition will be added
            Assert.AreEqual(0, this.generatedThings.Count);
        }

        [Test]
        public void VerifyThatStressGeneratorPropertiesAreSets()
        {
            Assert.AreEqual(this.sourceViewModel.Object, this.stressGeneratorViewModel.SourceViewModel);
            Assert.AreEqual(1, this.stressGeneratorViewModel.TimeInterval);
            Assert.AreEqual(5, this.stressGeneratorViewModel.TestObjectsNumber);
            Assert.AreEqual("ElementDefinition", this.stressGeneratorViewModel.ElementName);
            Assert.AreEqual("ED", this.stressGeneratorViewModel.ElementShortName);
            Assert.AreEqual(this.engineeringModelSetup, this.stressGeneratorViewModel.SelectedEngineeringModelSetup);
        }

        [Test]
        public void VerifyThatStressGeneratorPropertiesAreSetsWithMinimumNumberOfTestObjects()
        {
            this.stressGeneratorViewModel.TestObjectsNumber = short.MinValue;

            this.assembler.Cache.TryAdd(new CacheKey(this.iteration.Iid, null),
                new Lazy<CDP4Common.CommonData.Thing>(() => this.iteration));
            this.session.Setup(x => x.ActivePerson).Returns(this.person);
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain, null)}
                });

            Assert.DoesNotThrow(() => this.stressGeneratorViewModel.StressCommand.Execute(null));

            // Iteration will be modified
            Assert.AreEqual(1, this.modifiedThings.Count);

            // Five element definition and five parameters will be added
            Assert.AreEqual(10, this.generatedThings.Count);
        }

        [Test]
        public void VerifyThatStressGeneratorWorksIfOpenIterationIsPresent()
        {
            this.assembler.Cache.TryAdd(new CacheKey(this.iteration.Iid, null),
                new Lazy<CDP4Common.CommonData.Thing>(() => this.iteration));
            this.session.Setup(x => x.ActivePerson).Returns(this.person);
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain, null)}
                });

            Assert.DoesNotThrow(() => this.stressGeneratorViewModel.StressCommand.Execute(null));

            // Iteration will be modified
            Assert.AreEqual(1, this.modifiedThings.Count);

            // Five element definition and five parameters will be added
            Assert.AreEqual(this.stressGeneratorViewModel.TestObjectsNumber * 2, this.generatedThings.Count);
        }

        [Test]
        public void VerifyThatParameterGeneratorValueSetBaseUpdates()
        {
            var valueSet = new List<IValueSet>()
            {
                new CDP4Common.EngineeringModelData.ParameterValueSet(Guid.NewGuid(), null, null)
            };

            var clone = ParameterGenerator.UpdateValueSets(valueSet, ParameterSwitchKind.MANUAL, "1");

            Assert.AreEqual("{\"1\"}", clone.Manual.ToString());
        }
    }
}