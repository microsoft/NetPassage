// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="ConnectionSettings.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>

namespace Microsoft.HybridConnections.Core
{
    public class ConnectionSettings
    {
        public string HybridConnection { get; set; }
        public string PolicyName { get; set; }
        public string PolicyKey { get; set; }
        public string TargetHttp { get; set; }
    }
}
