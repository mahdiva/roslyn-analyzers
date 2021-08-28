﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseAsyncMethodInAsyncContext,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseAsyncMethodInAsyncContext,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseAsyncMethodInAsyncContextTests
    {

        [Fact]
        public async Task TaskWaitInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task t = null;
        {|#0:t.Wait()|};
        return Task.FromResult(1);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim t As Task = Nothing
        {|#0:t.Wait()|}
        Return Task.FromResult(1)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskWaitInValueTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    ValueTask T() {
        Task t = null;
        {|#0:t.Wait()|};
        return default;
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As ValueTask
        Dim t As Task = Nothing
        {|#0:t.Wait()|}
        Return CType(Nothing, ValueTask)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskWait_InIAsyncEnumerableAsyncMethod_ShouldReportWarning()
        {
            var testCS = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Test {
    async IAsyncEnumerable<int> FooAsync()
    {
        {|#0:Task.Delay(TimeSpan.FromSeconds(5)).Wait()|};
        yield return 1;
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Default.AddPackages(
                    ImmutableArray.Create(new PackageIdentity("Microsoft.Bcl.AsyncInterfaces", "5.0.0"))
                ),
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks

Module Program
    Sub Main()
        FooAsync()
    End Sub
    Function FooAsync() As IAsyncEnumerable(Of Integer)
        {|#0:Task.Delay(TimeSpan.FromSeconds(5)).Wait()|}
        Return Nothing
    End Function
End Module
";

            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Default.AddPackages(
                    ImmutableArray.Create(new PackageIdentity("Microsoft.Bcl.AsyncInterfaces", "5.0.0"))
                ),
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") },
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task<int> T() {
        Task<int> t = null;
        int result = {|#0:t.Result|};
        return Task.FromResult(result);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task(Of Integer)
        Dim t As Task(Of Integer) = Nothing
        Dim result = {|#0:t.Result()|}
        Return Task.FromResult(result)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning_FixPreservesCall()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task<int> t = null;
        Assert.NotNull({|#0:t.Result|});
        return Task.CompletedTask;
    }
}

static class Assert {
    internal static void NotNull(object value) => throw null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim t As Task(Of Integer) = Nothing
        Assert.NotNull({|#0:t.Result|})
        Return Task.CompletedTask
    End Function
End Module

Module Assert
    Friend Sub NotNull(value As Object)
        Throw New System.Exception()
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningAnonymousMethodWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<Task<int>> f = delegate {
            Task<int> t = null;
            int result = {|#0:t.Result|};
            return Task.FromResult(result);
        };
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim f As Func(Of Task(Of Integer)) = Function()
                                                 Dim t As Task(Of Integer) = Nothing
                                                 Dim result As Integer = {|#0:t.Result|}
                                                 Return Task.FromResult(result)
                                             End Function
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningSimpleLambdaWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<int, Task<int>> f = a => {
            Task<int> t = null;
            int result = {|#0:t.Result|};
            return Task.FromResult(result);
        };
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim f = Function(a)
                    Dim t As Task(Of Integer) = Nothing
                    Dim result As Integer = {|#0:t.Result|}
                    Return Task.FromResult(result)
                End Function
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningSimpleLambdaExpressionWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Task<int> b = null;
        Func<int, Task<int>> f = a => Task.FromResult({|#0:b.Result|});
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim b As Task(Of Integer) = Nothing
        Dim f = Function(a) Task.FromResult({|#0:b.Result|})
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning_FixRewritesCorrectExpression()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    async Task T() {
        await {|#0:Task.Run(() => Console.Error).Result|}.WriteLineAsync();
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Async Function Test() As Task
        Await {|#0:Task.Run(Function() Console.Error).Result|}.WriteLineAsync()
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningParentheticalLambdaWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<Task<int>> f = () => {
            Task<int> t = null;
            int result = {|#0:t.Result|};
            return Task.FromResult(result);
        };
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim f As Func(Of Task(Of Integer)) = Function()
                                                 Dim t As Task(Of Integer) = Nothing
                                                 Dim result As Integer = {|#0:t.Result|}
                                                 Return Task.FromResult(result)
                                             End Function
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Result") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodAnonymousDelegate_GeneratesNoWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    Task<int> T() {
        Task<int> task = null;
        task.ContinueWith(t => { Console.WriteLine(t.Result); });
        return Task.FromResult(1);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task(Of Integer)
        Dim task As Task(Of Integer) = Nothing
        task.ContinueWith(Sub(t) Console.WriteLine(t.Result))
        Return task.FromResult(1)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task TaskGetAwaiterGetResultInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task t = null;
        {|#0:t.GetAwaiter().GetResult()|};
        return Task.FromResult(1);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("GetResult") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim t As Task = Nothing
        {|#0:t.GetAwaiter().GetResult()|}
        Return task.FromResult(1)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("GetResult") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInSameTypeGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
class Test {
    Task T() {
        {|#0:Foo(10, 15)|};
        return Task.FromResult(1);
    }
    internal static void Foo(int x, int y) { }
    internal static Task FooAsync(int x, int y) => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo(10, 15)", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        {|#0:Foo(10, 15)|}
        Return Task.FromResult(1)
    End Function

    Friend Sub Foo(x As Integer, y As Integer)
    End Sub
    Friend Function FooAsync(x As Integer, y As Integer) As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo(10, 15)", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionIsObsolete_GeneratesNoWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    Task T() {
        Foo(10, 15);
        return Task.FromResult(1);
    }

    internal static void Foo(int x, int y) { }
    [Obsolete]
    internal static Task FooAsync(int x, int y) => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Foo(10, 15)
        Return Task.FromResult(1)
    End Function

    Friend Sub Foo(x As Integer, y As Integer)
    End Sub
    <Obsolete>
    Friend Function FooAsync(x As Integer, y As Integer) As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionIsPartlyObsolete_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    Task T() {
        {|#0:Foo(10, 15.0)|};
        return Task.FromResult(1);
    }

    internal static void Foo(int x, int y) { }
    internal static void Foo(int x, double y) { }
    [Obsolete]
    internal static Task FooAsync(int x, int y) => null;
    internal static Task FooAsync(int x, double y) => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo(10, 15.0)", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        {|#0:Foo(10, 15.0)|}
        Return Task.FromResult(1)
    End Function

    Friend Sub Foo(x As Integer, y As Integer)
    End Sub
    Friend Sub Foo(x As Integer, y As Double)
    End Sub
    <Obsolete>
    Friend Function FooAsync(x As Integer, y As Integer) As Task
        Return Nothing
    End Function
    Friend Function FooAsync(x As Integer, y As Double) As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo(10, 15.0)", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInSubExpressionGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        int r = {|#0:Foo()|}.CompareTo(1);
        return Task.FromResult(1);
    }

    internal static int Foo() => 5;
    internal static Task<int> FooAsync() => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo()", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim r = {|#0:Foo()|}.CompareTo(1)
        Return Task.FromResult(1)
    End Function

    Friend Function Foo() As Integer
        Return 5
    End Function
    Friend Function FooAsync() As Task(Of Integer)
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInOtherTypeGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        {|#0:Util.Foo()|};
        return Task.FromResult(1);
    }
}

class Util {
    internal static void Foo() { }
    internal static Task FooAsync() => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Util.Foo()", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        {|#0:Util.Foo()|}
        Return Task.FromResult(1)
    End Function
End Module

Module Util
    Friend Sub Foo()
    End Sub
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Util.Foo()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsAsPrivateInOtherTypeGeneratesNoWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Util.Foo();
        return Task.FromResult(1);
    }
}

class Util {
    internal static void Foo() { }
    private static Task FooAsync() => null;
}
";

            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Util.Foo()
        Return Task.FromResult(1)
    End Function
End Module

Module Util
    Friend Sub Foo()
    End Sub
    Private Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInOtherBaseTypeGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Apple a = null;
        {|#0:a.Foo()|};
        return Task.FromResult(1);
    }
}

class Fruit {
    internal Task FooAsync() => null;
}

class Apple : Fruit {
    internal void Foo() { }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("a.Foo()", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim a As Apple = Nothing
        {|#0:a.Foo()|}
        Return Task.FromResult(1)
    End Function
End Module

Class Fruit
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Class
Class Apple
    Inherits Fruit
    Friend Sub Foo()
    End Sub
End Class
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("a.Foo()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInExtensionMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Fruit f = null;
        {|#0:f.Foo()|};
        return Task.FromResult(1);
    }
}

class Fruit {
    internal void Foo() { }
}

static class FruitUtils {
    internal static Task FooAsync(this Fruit f) => null;
}
";

            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("f.Foo()", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports System.Runtime.CompilerServices
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim f As Fruit = Nothing
        {|#0:f.Foo()|}
        Return Task.FromResult(1)
    End Function
End Module

Class Fruit
    Friend Sub Foo()
    End Sub
End Class
Module FruitUtils
    <Extension()>
    Friend Function FooAsync(f As Fruit) As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("f.Foo()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationUsingStaticGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using static FruitUtils;

class Test {
    Task T() {
        {|#0:Foo()|};
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo() { }
    internal static Task FooAsync() => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo()", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        {|#0:Foo()|}
        Return Task.FromResult(1)
    End Function
End Module

Module FruitUtils
    Friend Sub Foo()
    End Sub
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task SyncInvocationUsingStaticGeneratesNoWarningAcrossTypes()
        {
            var testCS = @"
using System.Threading.Tasks;
using static FruitUtils;
using static PlateUtils;

class Test {
    Task T() {
        // Foo and FooAsync are totally different methods (on different types).
        // The use of Foo should therefore not produce a recommendation to use FooAsync,
        // despite their name similarities.
        Foo();
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo() { }
}

static class PlateUtils {
    internal static Task FooAsync() => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Foo()
        Return Task.FromResult(1)
    End Function
End Module

Module FruitUtils
    Friend Sub Foo()
    End Sub
End Module

Module PlateUtils
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task AwaitingAsyncMethodWithoutSuffixProducesNoWarningWhereSuffixVersionExists()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task Foo() => null;
    Task FooAsync() => null;

    async Task BarAsync() {
       await Foo();
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub
    Function Foo() As Task
        Return Nothing
    End Function
    Function FooAsync() As Task
        Return Nothing
    End Function
    Async Function BarAsync() As Task
        Await Foo()
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        /// <summary>
        /// Verifies that when method invocations and member access happens in properties
        /// (which can never be async), nothing bad happens.
        /// </summary>
        [Fact]
        public async Task NoDiagnosticAndNoExceptionForProperties()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    string Foo => string.Empty;
    string Bar => string.Join(""a"", string.Empty);
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()

    End Sub
    Function Foo() As String
        Return String.Empty
    End Function

    Function Bar() As String
        Return String.Join(""a"", String.Empty)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task GenericMethodName()
        {
            var testCS = @"
using System.Threading.Tasks;
using static FruitUtils;

class Test {
    Task T() {
        {|#0:Foo<int>()|};
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo<T>() { }
    internal static Task FooAsync<T>() => null;
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo<int>()", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        {|#0:Foo(Of Integer)()|}
        Return Task.FromResult(1)
    End Function
End Module

Module FruitUtils
    Friend Sub Foo(Of t)()
    End Sub
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo(Of Integer)()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task AsyncAlternative_CodeFixRespectsTrivia()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void Foo() { }
    Task FooAsync() => Task.CompletedTask;

    async Task DoWorkAsync()
    {
        await Task.Yield();
        Console.WriteLine(""Foo"");

        // Some comment
        {|#0:Foo(/*argcomment*/)|}; // another comment
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo(/*argcomment*/)", "FooAsync") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub
    Friend Sub Foo()
    End Sub

    Friend Function FooAsync() As Task
        Return Task.CompletedTask
    End Function
    Async Function DoWorkAsync() As Task
        Await Task.Yield()
        Console.WriteLine(""Foo"")

        'Some comment
        {|#0:Foo()|} 'another comment
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.Descriptor).WithLocation(0).WithArguments("Foo()", "FooAsync") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task AwaitRatherThanWait_CodeFixRespectsTrivia()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void Foo() { }
    Task FooAsync() => Task.CompletedTask;

    async Task DoWorkAsync()
    {
        await Task.Yield();
        System.Console.WriteLine(""Foo"");

        // Some comment
        {|#0:FooAsync(/*argcomment*/).Wait()|};
    }
}
";

            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") }
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub
    Friend Sub Foo()
    End Sub

    Friend Function FooAsync() As Task
        Return Task.CompletedTask
    End Function
    Async Function DoWorkAsync() As Task
        Await Task.Yield()
        Console.WriteLine(""Foo"")

        'Some comment
        {|#0:FooAsync().Wait()|} 'another comment
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(UseAsyncMethodInAsyncContext.DescriptorNoAlternativeMethod).WithLocation(0).WithArguments("Wait") }
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task DoNotSuggestAsyncAlternativeWhenItIsSelf()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    public async Task CallMainAsync()
    {
        // do stuff
        CallMain();
        // do stuff
    }

    public void CallMain()
    {
        // more stuff
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub

    Async Function CallMainAsync() As Task
        CallMain()
    End Function
    Sub CallMain()
        'more stuff
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task DoNotSuggestAsyncAlternativeWhenItReturnsVoid()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void LogInformation() { }
    void LogInformationAsync() { }

    Task MethodAsync()
    {
        LogInformation();
        return Task.CompletedTask;
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                TestCode = testCS,
            };

            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub

    Sub LogInformation()
    End Sub

    Sub LogInformationAsync()
    End Sub
    Function MethodAsync() As Task
        LogInformation()
        Return Task.CompletedTask
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                TestCode = testVB,
            };

            await vbTestVerify.RunAsync();
        }
    }
}