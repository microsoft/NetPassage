// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="StringExtensions.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>

namespace Microsoft.HybridConnections.Core.Extensions
{
    using System;
    public static class StringExtensions
    {
        /// <summary>
        /// Ensures the given string ends with the requested pattern. If it does no allocations are performed.
        /// </summary>
        public static string EnsureEndsWith(this string s, string value, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (!string.IsNullOrEmpty(s) && s.EndsWith(value, comparisonType))
            {
                return s;
            }

            return s + value;
        }
    }
}
