﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IterationGenerator.cs" company="RHEA System S.A.">
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
    using System.Linq;
    using System.Threading.Tasks;
    using CDP4Common.EngineeringModelData;
    using CDP4Common.SiteDirectoryData;
    using CDP4Dal;
    using ViewModels;

    /// <summary>
    /// Helper class for creating iteration <see cref="Iteration"/> for test purposes.
    /// </summary>
    internal static class IterationGenerator
    {
        /// <summary>
        /// Open last <see cref="Iteration"/> of the given <paramref name="modelSetup"/>.
        /// </summary>
        /// <param name="session">
        /// Server <see cref="ISession"/>
        /// </param>
        /// <param name="modelSetup">
        /// <see cref="EngineeringModelSetup"/> that iteration belongs to.
        /// </param>
        /// <returns>
        /// An <see cref="Iteration"/>.
        /// </returns>
        public static async Task<Iteration> OpenLastIteration(ISession session, EngineeringModelSetup modelSetup)
        {
            var model = new EngineeringModel(
                modelSetup.EngineeringModelIid,
                session.Assembler.Cache,
                session.Credentials.Uri)
            {
                EngineeringModelSetup = modelSetup
            };

            var lastIterationSetup = modelSetup.IterationSetup
                .OrderBy(iterationSetups => iterationSetups.IterationNumber)
                .Single(iterationSetup => !iterationSetup.IsDeleted && iterationSetup.FrozenOn == null);

            var lastIteration = new Iteration(
                lastIterationSetup.IterationIid,
                session.Assembler.Cache,
                session.Credentials.Uri)
            {
                IterationSetup = lastIterationSetup
            };

            model.Iteration.Add(lastIteration);

            await session.Read(lastIteration, session.ActivePerson.DefaultDomain);

            return lastIteration;
        }

        /// <summary>
        /// Check that the <see cref="EngineeringModelSetup"/> references the required <see cref="ReferenceDataLibrary"/>.
        /// </summary>
        /// <param name="iteration">
        /// An <see cref="Iteration"/>.
        /// </param>
        /// <returns>
        /// True if iteration references the required <see cref="ReferenceDataLibrary"/>, false otherwise.
        /// </returns>
        public static bool IterationReferencesGenericRdl(Iteration iteration)
        {
            var model = iteration.Container as EngineeringModel;
            var modelRdl = model?.EngineeringModelSetup.RequiredRdl.FirstOrDefault();
            var chainedRdl = modelRdl?.RequiredRdl;

            while (chainedRdl != null)
            {
                if (chainedRdl.ShortName == StressGeneratorConfiguration.GenericRdlShortName)
                {
                    return true;
                }

                chainedRdl = chainedRdl.RequiredRdl;
            }

            return false;
        }
    }
}
