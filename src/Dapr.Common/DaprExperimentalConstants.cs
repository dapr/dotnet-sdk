// ________________________________________________________________________
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE_2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ________________________________________________________________________

namespace Dapr.Common;

/// <summary>
/// Reflects constants available to use in the experimental attributes in various Dapr packages.
/// </summary>
public static class DaprExperimentalConstants
{
    private const string DaprPrefix = "DAPR";
    
    private const string MessagingPrefix = $"{DaprPrefix}_MESSAGING";
    private const string WorkflowPrefix = $"{DaprPrefix}_WORKFLOW";
    private const string BindingPrefix = $"{DaprPrefix}_BINDING";
    private const string CryptoPrefix = $"{DaprPrefix}_CRYPTO";
    private const string StatePrefix = $"{DaprPrefix}_STATE";
    private const string AiPrefix = $"{DaprPrefix}_AI";
    private const string InvocationPrefix = $"{DaprPrefix}_INVOCATION";
    private const string ActorsPrefix = $"{DaprPrefix}_ACTORS";
    private const string JobsPrefix = $"{DaprPrefix}_JOBS";
    private const string LocksPrefix = $"{DaprPrefix}_LOCK";

    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Messaging package.
    /// </summary>
    public const string MessagingIdentifier = $"{MessagingPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Workflow package.
    /// </summary>
    public const string WorkflowIdentifier = $"{WorkflowPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Binding package.
    /// </summary>
    public const string BindingIdentifier = $"{BindingPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Cryptography package.
    /// </summary>
    public const string CryptographyIdentifier = $"{CryptoPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.State package.
    /// </summary>
    public const string StateIdentifier = $"{StatePrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.AI package.
    /// </summary>
    public const string AiIdentifier = $"{AiPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Invocation package.
    /// </summary>
    public const string InvocationIdentifier = $"{InvocationPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Actors package.
    /// </summary>
    public const string ActorsIdentifier = $"{ActorsPrefix}_0001";
    
    /// <summary>
    /// Identifier used for experimental flags in the Dapr.Jobs package.
    /// </summary>
    public const string JobsIdentifier = $"{JobsPrefix}_0001";

    /// <summary>
    /// Identifier used for experimental flags in the Dapr.DistributedLock package.
    /// </summary>
    public const string LockIdentifier = $"{LocksPrefix}_0001";
}
