// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseAsyncInAsync,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
//using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
//    Microsoft.NetCore.Analyzers.Runtime.BufferBlockCopyLengthAnalyzer,
//    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseAsyncInAsyncTests
    {
        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInSameTypeGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    Task T() {
        [|Foo(10, 15)|];
        return Task.FromResult(1);
    }
    internal static void Foo(int x, int y) { }
    internal static Task FooAsync(int x, int y) => null;
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task DoNotSuggestAsyncAlternativeWhenItIsSelf()
        {
            var test = @"
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task AwaitingAsyncMethodWithoutSuffixProducesNoWarningWhereSuffixVersionExists()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task Foo() => null;
    Task FooAsync() => null;

    async Task BarAsync() {
       await Foo();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticAndNoExceptionForProperties()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    string Foo => string.Empty;
    string Bar => string.Join(""a"", string.Empty);
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task<int> T() {
        Task<int> t = null;
        int result = [|t.Result|];
        return Task.FromResult(result);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task GenericMethodName()
        {
            var test = @"
using System.Threading.Tasks;
using static FruitUtils;

class Test {
    Task T() {
        [|Foo<int>()|];
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo<T>() { }
    internal static Task FooAsync<T>() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionIsObsolete_GeneratesNoWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Foo(10, 15);
        return Task.FromResult(1);
    }

    internal static void Foo(int x, int y) { }
    [System.Obsolete]
    internal static Task FooAsync(int x, int y) => null;
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionIsPartlyObsolete_GeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        [|Foo(10, 15.0)|];
        return Task.FromResult(1);
    }

    internal static void Foo(int x, int y) { }
    internal static void Foo(int x, double y) { }
    [System.Obsolete]
    internal static Task FooAsync(int x, int y) => null;
    internal static Task FooAsync(int x, double y) => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task DoNotSuggestAsyncAlternativeWhenItReturnsVoid()
        {
            var test = @"
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task AsyncAlternative_CodeFixRespectsTrivia()
        {
            var test = @"
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
        [|Foo(/*argcomment*/)|]; // another comment
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task TaskWaitInValueTaskReturningMethodGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    ValueTask T() {
        Task t = null;
        [|t.Wait()|];
        return default;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsAsPrivateInOtherTypeGeneratesNoWarning()
        {
            var test = @"
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInExtensionMethodGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Fruit f = null;
        [|f.Foo()|];
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SyncInvocationUsingStaticGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;
using static FruitUtils;

class Test {
    Task T() {
        [|Foo()|];
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo() { }
    internal static Task FooAsync() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task AwaitRatherThanWait_CodeFixRespectsTrivia()
        {
            var test = @"
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
        [|FooAsync(/*argcomment*/).Wait()|];
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInOtherTypeGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        [|Util.Foo()|];
        return Task.FromResult(1);
    }
}

class Util {
    internal static void Foo() { }
    internal static Task FooAsync() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }



        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInOtherBaseTypeGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Apple a = null;
        [|a.Foo()|];
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }




        [Fact]
        public async Task SyncInvocationUsingStaticGeneratesNoWarningAcrossTypes()
        {
            var test = @"
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

#if !NETCOREAPP
        [Fact]
        public async Task JTFRunInAsyncMethodGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    async Task T() {
        JoinableTaskFactory jtf = null;
        jtf.Run(() => TplExtensions.CompletedTask);
        [|this.Run()|];
    }

    void Run() { }
}
";
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = test,
            };
            await csharpTest.RunAsync();
        }
#endif

        [Fact]
        public async Task TaskWaitInTaskReturningMethodGeneratesWarning()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task t = null;
        [|t.Wait()|];
        return Task.FromResult(1);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningAnonymousMethodWithinSyncMethod_GeneratesWarning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<Task<int>> f = delegate {
            Task<int> t = null;
            int result = [|t.Result|];
            return Task.FromResult(result);
        };
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodAnonymousDelegate_GeneratesNoWarning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    Task<int> T() {
        {
        Task<int> task = null;
        task.ContinueWith(t => { Console.WriteLine(t.Result); });
        return Task.FromResult(1);
        }
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}