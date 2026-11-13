using AIpoweredVivaExamSystem.Web.Pages.Questions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class WebDependencyTests
{
    [Fact]
    public void Web_assembly_does_not_reference_domain()
    {
        var referencedAssemblies = typeof(CreateModel).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies,
            assembly => assembly.Name == "AIpoweredVivaExamSystem.Domain");
    }
}
