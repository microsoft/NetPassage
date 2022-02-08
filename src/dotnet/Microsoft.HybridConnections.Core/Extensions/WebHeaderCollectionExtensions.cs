// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="WebHeaderCollectionExtensions.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>


namespace Microsoft.HybridConnections.Core.Extensions
{
    using System.Collections.Generic;
    public static class WebHeaderCollectionExtensions
    {
        public static IEnumerable<KeyValuePair<string, string>> GetHeaders(this System.Net.WebHeaderCollection webHeaderCollection)
        {
            string[] keys = webHeaderCollection.AllKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                yield return new KeyValuePair<string, string>(keys[i], webHeaderCollection[keys[i]]);
            }
        }
    }
}
