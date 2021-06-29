﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EngineeringModelSetupGenerator.cs" company="RHEA System S.A.">
//    Copyright (c) 2015-2020 RHEA System S.A.
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

namespace StressGenerator.Utils
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using CDP4Common.EngineeringModelData;
    using CDP4Dal.Operations;
    using CDP4Common.SiteDirectoryData;
    using CDP4Dal;
    using Common.Events;

    /// <summary>
    /// Helper class for creating iteration <see cref="Iteration"/> for test purposes
    /// </summary>
    internal static class EngineeringModelSetupGenerator
    {
        /// <summary>
        /// Create a new instance of <see cref="Iteration" />
        /// </summary>
        /// <param name="session">
        /// Server session <see cref="ISession"/>
        /// </param>
        /// <param name="modelName">
        /// EngineeringModelSetup name
        /// </param>
        /// <param name="sourceEngineeringModelSetup">
        /// The source EngineeringModelSetup <see cref="EngineeringModelSetup"/>
        /// </param>
        /// <returns>
        /// An instance of <see cref="EngineeringModelSetup"/>
        /// </returns>
        public static async Task<EngineeringModelSetup> Create(
            ISession session,
            string modelName,
            EngineeringModelSetup sourceEngineeringModelSetup = null)
        {
            var siteDirectory = session.RetrieveSiteDirectory();
            var siteDirectoryCloned = siteDirectory.Clone(false);

            var engineeringModelSetup = new EngineeringModelSetup(
                Guid.NewGuid(),
                session.Assembler.Cache,
                session.Credentials.Uri)
            {
                Name = modelName,
                ShortName = modelName,
                EngineeringModelIid = Guid.NewGuid(),
            };

            if (sourceEngineeringModelSetup == null)
            {
                var modelReferenceDataLibrary = new ModelReferenceDataLibrary(
                    Guid.NewGuid(),
                    session.Assembler.Cache,
                    session.Credentials.Uri)
                    {
                        Name = $"{modelName} MODEL RDL",
                        ShortName = $"{modelName} MRDL",
                        RequiredRdl = siteDirectory.SiteReferenceDataLibrary.FirstOrDefault()
                    };
                engineeringModelSetup.RequiredRdl.Add(modelReferenceDataLibrary);
            }
            else
            {
                engineeringModelSetup.SourceEngineeringModelSetupIid = sourceEngineeringModelSetup.Iid;
            }

            siteDirectoryCloned.Model.Add(engineeringModelSetup);

            engineeringModelSetup = await Write(session, engineeringModelSetup, siteDirectory, siteDirectoryCloned);

            return engineeringModelSetup;
        }

        /// <summary>
        /// Write new EngineeringModelSetup
        /// </summary>
        /// <param name="session">
        /// Server session <see cref="ISession"/>
        /// </param>
        /// <param name="engineeringModelSetup">
        /// The EngineeringModelSetup <see cref="EngineeringModelSetup"/>
        /// </param>
        /// <param name="siteDirectory">
        /// Current site directory used for creating write transaction <see cref="SiteDirectory"/>
        /// </param>
        /// <param name="siteDirectoryCloned">
        /// Cloned site directory used for creating write transaction <see cref="SiteDirectory"/>
        /// </param>
        /// <returns>
        /// An instance of <see cref="EngineeringModelSetup"/>
        /// </returns>
        private static async Task<EngineeringModelSetup> Write(
            ISession session,
            EngineeringModelSetup engineeringModelSetup,
            SiteDirectory siteDirectory,
            SiteDirectory siteDirectoryCloned)
        {
            try
            {
                var transactionContext = TransactionContextResolver.ResolveContext(siteDirectory);
                var operationContainer = new OperationContainer(transactionContext.ContextRoute());

                operationContainer.AddOperation(new Operation(
                    siteDirectory.ToDto(), 
                    siteDirectoryCloned.ToDto(),
                    OperationKind.Update));
                operationContainer.AddOperation(new Operation(
                    null,
                    engineeringModelSetup.ToDto(),
                    OperationKind.Create));

                if (engineeringModelSetup.RequiredRdl.Count != 0)
                {
                    operationContainer.AddOperation(new Operation(
                        null,
                        engineeringModelSetup.RequiredRdl.FirstOrDefault()?.ToDto(),
                        OperationKind.Create));
                }

                await session.Dal.Write(operationContainer);

                CDPMessageBus.Current.SendMessage(new LogEvent
                {
                    Message = $"Successfully generated EngineeringModelSetup {engineeringModelSetup.Name} ({engineeringModelSetup.ShortName}).",
                    Verbosity = LogVerbosity.Info
                });
            }
            catch (Exception ex)
            {
                CDPMessageBus.Current.SendMessage(new LogEvent
                {
                    Message = $"Cannot generate EngineeringModelSetup {engineeringModelSetup.Name} ({engineeringModelSetup.ShortName}). Exception: {ex.Message}",
                    Exception = ex,
                    Verbosity = LogVerbosity.Error
                });

                engineeringModelSetup = null;
            }

            return engineeringModelSetup;
        }

        /// <summary>
        /// Delete an existing EngineeringModelSetup
        /// </summary>
        /// <param name="session">
        /// Server session <see cref="ISession"/>
        /// </param>
        /// <param name="engineeringModelSetup">
        /// The EngineeringModelSetup <see cref="EngineeringModelSetup"/>
        /// </param>
        public static async Task Delete(ISession session, EngineeringModelSetup engineeringModelSetup)
        {
            try
            {
                var siteDirectory = session.RetrieveSiteDirectory();
                var siteDirectoryCloned = siteDirectory.Clone(true);

                var transactionContext = TransactionContextResolver.ResolveContext(siteDirectory);
                var operationContainer = new OperationContainer(transactionContext.ContextRoute());

                operationContainer.AddOperation(new Operation(
                    siteDirectory.ToDto(),
                    siteDirectoryCloned.ToDto(),
                    OperationKind.Update));
                operationContainer.AddOperation(new Operation(
                    null,
                    engineeringModelSetup.ToDto(),
                    OperationKind.Delete));

                await session.Dal.Write(operationContainer);
            }
            catch (Exception ex)
            {
                CDPMessageBus.Current.SendMessage(new LogEvent
                {
                    Message = $"Cannot delete EngineeringModelSetup {engineeringModelSetup.Name} ({engineeringModelSetup.ShortName}). Exception: {ex.Message}",
                    Exception = ex,
                    Verbosity = LogVerbosity.Error
                });
            }
        }
    }
}