﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// Check for the intended use of .Length on arrays passed into Buffer.BlockCopy
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class BufferBlockCopyLengthAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2018";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.BufferBlockCopyLengthTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.BufferBlockCopyLengthMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.BufferBlockCopyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Reliability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemBuffer, out INamedTypeSymbol? bufferType))
                {
                    return;
                }

                INamedTypeSymbol arrayType = context.Compilation.GetSpecialType(SpecialType.System_Array);
                INamedTypeSymbol int32Type = context.Compilation.GetSpecialType(SpecialType.System_Int32);

                // Buffer.BlockCopy(Array src, int srcOffset, Array dst, int dstOffset, int count)
                ParameterInfo[] blockCopyParameterInfo = {
                    ParameterInfo.GetParameterInfo(arrayType, false, 1, false),
                    ParameterInfo.GetParameterInfo(int32Type, false, 1, false),
                    ParameterInfo.GetParameterInfo(arrayType, false, 1, false),
                    ParameterInfo.GetParameterInfo(int32Type, false, 1, false),
                    ParameterInfo.GetParameterInfo(int32Type, false, 1, false)};

                IMethodSymbol? blockCopyMethod = bufferType
                   .GetMembers("BlockCopy")
                   .OfType<IMethodSymbol>()
                   .GetFirstOrDefaultMemberWithParameterInfos(blockCopyParameterInfo);

                if (blockCopyMethod is null)
                {
                    return;
                }

                IPropertySymbol arrayLengthProperty = arrayType
                    .GetMembers("Length")
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault();

                context.RegisterOperationAction(context =>
                {
                    var invocationOperation = (IInvocationOperation)context.Operation;

                    if (!invocationOperation.TargetMethod.Equals(blockCopyMethod))
                    {
                        return;
                    }

                    ImmutableArray<IArgumentOperation> arguments = IOperationExtensions.GetArgumentsInParameterOrder(invocationOperation.Arguments);

                    IArgumentOperation sourceArgument = arguments[0];
                    IArgumentOperation destinationArgument = arguments[2];
                    IArgumentOperation countArgument = arguments[4];

                    bool CheckArrayLengthLocalReference(IArgumentOperation targetArgument, IPropertyReferenceOperation lengthPropertyArgument)
                    {
                        if (targetArgument.Value is IConversionOperation targetArgValue)
                        {
                            if (lengthPropertyArgument.Instance.GetReferencedMemberOrLocalOrParameter() == targetArgValue.Operand.GetReferencedMemberOrLocalOrParameter())
                            {
                                IArrayTypeSymbol countArgumentArrayTypeSymbol = (IArrayTypeSymbol)lengthPropertyArgument.Instance.Type;
                                if (countArgumentArrayTypeSymbol.ElementType.SpecialType != SpecialType.System_Byte &&
                                countArgumentArrayTypeSymbol.ElementType.SpecialType != SpecialType.System_SByte)
                                {
                                    return true;
                                }
                            }
                        }
                        else if (targetArgument.Value is ILocalReferenceOperation local)
                        {
                            if (lengthPropertyArgument.Instance.GetReferencedMemberOrLocalOrParameter() == local.Local)
                            {
                                return true;
                            }
                        }
                        return false;
                    }

                    bool CheckLengthPropertyOnByteOrSByteArrays(IPropertyReferenceOperation countArgument)
                    {
                        if (countArgument.Property.Equals(arrayLengthProperty))
                        {
                            return CheckArrayLengthLocalReference(sourceArgument, countArgument) || CheckArrayLengthLocalReference(destinationArgument, countArgument);
                        }

                        return false;
                    }

                    if (countArgument.Value is IPropertyReferenceOperation countArgumentValue && CheckLengthPropertyOnByteOrSByteArrays(countArgumentValue))
                    {
                        context.ReportDiagnostic(countArgument.Value.CreateDiagnostic(Rule));
                    }
                    else
                    {
                        if (countArgument.Value is not ILocalReferenceOperation localReferenceOperation)
                        {
                            return;
                        }

                        SemanticModel semanticModel = countArgument.SemanticModel;
                        CancellationToken cancellationToken = context.CancellationToken;

                        ILocalSymbol localArgumentDeclaration = localReferenceOperation.Local;

                        SyntaxReference declaringSyntaxReference = localArgumentDeclaration.DeclaringSyntaxReferences.FirstOrDefault();
                        if (declaringSyntaxReference is null)
                        {
                            return;
                        }

                        if (semanticModel.GetOperationWalkingUpParentChain(declaringSyntaxReference.GetSyntax(cancellationToken), cancellationToken) is not IVariableDeclaratorOperation variableDeclaratorOperation)
                        {
                            return;
                        }

                        IVariableInitializerOperation variableInitializer = variableDeclaratorOperation.Initializer;

                        if (variableInitializer is not null && variableInitializer.Value is IPropertyReferenceOperation variableInitializerPropertyReference &&
                        CheckLengthPropertyOnByteOrSByteArrays(variableInitializerPropertyReference))
                        {
                            context.ReportDiagnostic(countArgument.Value.CreateDiagnostic(Rule));
                        }
                        else if (variableDeclaratorOperation.Parent is IVariableDeclarationOperation variableDeclarationOperation &&
                        variableDeclarationOperation.Initializer is not null &&
                        variableDeclarationOperation.Initializer.Value is IPropertyReferenceOperation variableInitializerPropertyReferenceVB &&
                        CheckLengthPropertyOnByteOrSByteArrays(variableInitializerPropertyReferenceVB))
                        {
                            context.ReportDiagnostic(countArgument.Value.CreateDiagnostic(Rule));
                        }
                    }
                },
                OperationKind.Invocation);
            });
        }
    }
}
