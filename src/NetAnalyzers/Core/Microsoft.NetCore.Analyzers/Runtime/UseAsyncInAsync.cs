// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// This analyzer recognizes invocations of JoinableTaskFactory.Run(Func{Task}), JoinableTask.Join(), and variants
    /// that occur within an async method, thus defeating a perfect opportunity to be asynchronous.
    /// </summary>
    /// <remarks>
    /// <![CDATA[
    ///   async Task MyMethod()
    ///   {
    ///     JoinableTaskFactory jtf;
    ///     jtf.Run(async delegate {  /* This analyzer will report warning on this JoinableTaskFactory.Run invocation. */
    ///       await Stuff();
    ///     });
    ///   }
    /// ]]>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseAsyncInAsync : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2018";
        internal const string AsyncMethodKeyName = "AsyncMethodName";
        internal const string ExtensionMethodNamespaceKeyName = "ExtensionMethodNamespace";
        internal const string MandatoryAsyncSuffix = "Async";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallAsyncMethodInAsyncContextTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallAsyncMethodInAsyncContextMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallAsyncMethodInAsyncContextMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Descriptor = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Reliability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor DescriptorNoAlternativeMethod = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              s_localizableMessage,
                                                                              DiagnosticCategory.Reliability,
                                                                              RuleLevel.BuildWarning,
                                                                              s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterOperationBlockStartAction(context =>
            {
                context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

                context.RegisterOperationAction(AnalyzePropertyGetter, OperationKind.PropertyReference);
            });
        }

        internal static void AnalyzePropertyGetter(OperationAnalysisContext context)
        {
            if (IsInTaskReturningMethodOrDelegate(context))
            {
                InspectMemberAccess(context, CommonInterests.SyncBlockingProperties);
            }
        }

        internal static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            if (IsInTaskReturningMethodOrDelegate(context))
            {
                if (context.Operation is not IInvocationOperation)
                {
                    return;
                }

                if (InspectMemberAccess(context, CommonInterests.SyncBlockingMethods))
                {
                    // Don't return double-diagnostics.
                    return;
                }

                // Also consider all method calls to check for Async-suffixed alternatives.
                var semanticModel = context.Operation.SemanticModel;
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(context.Operation.Syntax, context.CancellationToken);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol && !methodSymbol.Name.EndsWith(MandatoryAsyncSuffix, StringComparison.CurrentCulture) &&
                    !methodSymbol.HasAsyncCompatibleReturnType())
                {
                    string asyncMethodName = methodSymbol.Name + MandatoryAsyncSuffix;
                    ImmutableArray<ISymbol> symbols = semanticModel.LookupSymbols(
                        context.Operation.Syntax.GetLocation().SourceSpan.Start,
                        methodSymbol.ContainingType,
                        asyncMethodName,
                        includeReducedExtensionMethods: true);

                    string containingMethodName = "";
                    if (context.ContainingSymbol is IMethodSymbol parentMethod)
                    {
                        containingMethodName = parentMethod.Name;
                    }

                    if (context.Operation is not IInvocationOperation invOperation)
                    {
                        return;
                    }

                    SyntaxNode invokedMethodName = context.Operation.Syntax;
                    Location invokedMethodLocation = invOperation.Syntax.GetLocation();

                    foreach (IMethodSymbol m in symbols.OfType<IMethodSymbol>())
                    {
                        if (!m.IsObsolete()
                            && HasSupersetOfParameterTypes(m, methodSymbol)
                            && m.Name != containingMethodName
                            && m.HasAsyncCompatibleReturnType())
                        {
                            // An async alternative exists.
                            ImmutableDictionary<string, string>? properties = ImmutableDictionary<string, string>.Empty
                                .Add(AsyncMethodKeyName, asyncMethodName);

                            Diagnostic diagnostic = Diagnostic.Create(
                                Descriptor,
                                invokedMethodLocation,
                                properties,
                                invokedMethodName.ToString(),
                                asyncMethodName);
                            context.ReportDiagnostic(diagnostic);

                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the given method has parameters to cover all the parameter types in another method.
        /// </summary>
        /// <param name="candidateMethod">The candidate method.</param>
        /// <param name="baselineMethod">The baseline method.</param>
        /// <returns>
        ///   <c>true</c> if <paramref name="candidateMethod"/> has a superset of parameter types found in <paramref name="baselineMethod"/>; otherwise <c>false</c>.
        /// </returns>
        private static bool HasSupersetOfParameterTypes(IMethodSymbol candidateMethod, IMethodSymbol baselineMethod)
        {
            return candidateMethod.Parameters.All(candidateParameter => baselineMethod.Parameters.Any(baselineParameter => baselineParameter.Type?.Equals(candidateParameter.Type) ?? false));
        }

        private static IMethodSymbol GetParentMethodOrDelegate(OperationAnalysisContext context)
        {
            var containingAnonymousFunction = context.Operation.TryGetContainingAnonymousFunctionOrLocalFunction();
            if (containingAnonymousFunction is not null)
            {
                return containingAnonymousFunction;
            }

            ISymbol containingSymbol = context.ContainingSymbol;
            while (containingSymbol is not null && containingSymbol is not IMethodSymbol)
            {
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            IMethodSymbol parentMethod = (IMethodSymbol)containingSymbol;
            return parentMethod;
        }

        private static bool IsInTaskReturningMethodOrDelegate(OperationAnalysisContext context)
        {
            // We want to scan invocations that occur inside Task and Task<T>-returning delegates or methods.
            // That is: methods that either are or could be made async.
            IMethodSymbol parentMethod = GetParentMethodOrDelegate(context);
            if (parentMethod == null)
            {
                return false;
            }

            ITypeSymbol returnTypeSymbol = parentMethod.ReturnType;

            if (returnTypeSymbol is null)
            {
                return false;
            }

            return IsAsyncCompatibleReturnType(returnTypeSymbol);
        }

        private static readonly IReadOnlyList<string> SystemRuntimeCompilerServices = new[]
        {
            nameof(System),
            nameof(System.Runtime),
            nameof(System.Runtime.CompilerServices),
        };

        private static bool IsAsyncCompatibleReturnType(ITypeSymbol? typeSymbol)
        {
            if (typeSymbol is null)
            {
                return false;
            }

            // ValueTask and ValueTask<T> have the AsyncMethodBuilderAttribute
            return (typeSymbol.Name == nameof(Task) && typeSymbol.BelongsToNamespace(Namespaces.SystemThreadingTasks))
                || IsIAsyncEnumerable(typeSymbol) || typeSymbol.AllInterfaces.Any(IsIAsyncEnumerable)
                || typeSymbol.GetAttributes().Any(ad => ad.AttributeClass?.Name == nameof(System.Runtime.CompilerServices.AsyncMethodBuilderAttribute) &&
                ad.AttributeClass.BelongsToNamespace(SystemRuntimeCompilerServices));

            static bool IsIAsyncEnumerable(ITypeSymbol symbol)
                => symbol.Name == "IAsyncEnumerable"
                && symbol.BelongsToNamespace(Namespaces.SystemCollectionsGeneric);
        }

        private static bool InspectMemberAccess(OperationAnalysisContext context, IEnumerable<CommonInterests.SyncBlockingMethod> problematicMethods)
        {
            ISymbol? memberSymbol = context.Operation.SemanticModel.GetSymbolInfo(context.Operation.Syntax, context.CancellationToken).Symbol;
            if (memberSymbol is object)
            {
                foreach (CommonInterests.SyncBlockingMethod item in problematicMethods)
                {
                    if (item.Method.IsMatch(memberSymbol))
                    {
                        Location? location = context.Operation.Syntax.GetLocation();
                        ImmutableDictionary<string, string>? properties = ImmutableDictionary<string, string>.Empty
                                .Add(ExtensionMethodNamespaceKeyName, item.ExtensionMethodNamespace is object ? string.Join(".", item.ExtensionMethodNamespace) : string.Empty);
                        DiagnosticDescriptor descriptor;
                        var messageArgs = new List<object>(2)
                            {
                                item.Method.Name
                            };
                        if (item.AsyncAlternativeMethodName is object)
                        {
                            properties = properties.Add(AsyncMethodKeyName, item.AsyncAlternativeMethodName);
                            descriptor = Descriptor;
                            messageArgs.Add(item.AsyncAlternativeMethodName);
                        }
                        else
                        {
                            properties = properties.Add(AsyncMethodKeyName, string.Empty);
                            descriptor = DescriptorNoAlternativeMethod;
                        }

                        Diagnostic diagnostic = Diagnostic.Create(descriptor, location, properties, messageArgs.ToArray());
                        context.ReportDiagnostic(diagnostic);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
