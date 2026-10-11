using Dynasty.Core;
using NUnit.Framework;

namespace Dynasty.Tests.ProjectSetup
{
    public class ProjectSetupAssemblyTest
    {
        [Test]
        public void test_project_setup_core_type_resolves_to_dynasty_core_assembly()
        {
            // Arrange
            var coreType = typeof(DynastyCore);

            // Act
            var assemblyName = coreType.Assembly.GetName().Name;

            // Assert
            Assert.That(assemblyName, Is.EqualTo(DynastyCore.AssemblyName));
        }
    }
}
