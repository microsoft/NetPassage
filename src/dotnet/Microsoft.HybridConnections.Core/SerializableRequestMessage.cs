// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="SerializableRequestMessage.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>


namespace Microsoft.HybridConnections.Core
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class SerializableRequestMessage
    {
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public string Content { get; set; }
        public string HttpMethod { get; set; }
        public string RemoteEndPoint { get; set; }
        public string Url { get; set; }
        public string HybridConnectionScheme { get; set; }
        public string HybridConnectionName { get; set; }
    }
}
