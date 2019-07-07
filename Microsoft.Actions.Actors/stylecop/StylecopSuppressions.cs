// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "StyleCop.CSharp.MaintainabilityRules",
    "SA1119:StatementMustNotUseUnnecessaryParenthesis",
    Justification = "The code is more redable, especially when using parenthesis for the return statements.")]

[assembly: SuppressMessage(
    "StyleCop.CSharp.ReadabilityRules",
    "SA1124:DoNotUseRegions",
    Justification = "In large files region allows creating logical sections in the file for better readability.")]

[assembly: SuppressMessage(
    "StyleCop.CSharp.SpacingRules",
    "SA1028:Code should not contain trailing whitespace",
    Justification = "Documentation from swagger used when autogenerating may contain extra spaces")]

[assembly: SuppressMessage(
    "Style",
    "IDE1006:Naming Styles",
    Justification = "Prefer SA1307-SA1307AccessibleFieldsMustBeginWithUpperCaseLetter over IDE:1006.")]

[assembly: SuppressMessage(
    "StyleCop.CSharp.DocumentationRules",
    "SA1642:Constructor summary documentation should begin with standard text",
    Justification = "Disabling until codegen generates correct constructor documentation.")]

[assembly: SuppressMessage(
    "StyleCop.CSharp.DocumentationRules",
    "SA1625:Element documentation should not be copied and pasted",
    Justification = "Some of the parameter documentation is similar in REST swagger specification.")]
