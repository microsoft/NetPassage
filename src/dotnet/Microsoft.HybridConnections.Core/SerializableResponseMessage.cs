// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="SerializableResponseMessage.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>

namespace Microsoft.HybridConnections.Core
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class SerializableResponseMessage
    {
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public string Content { get; set; }
        public string ReasonPhrase { get; set; }
        public string StatusCode { get; set; }
        public string requestMessage { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }
}
