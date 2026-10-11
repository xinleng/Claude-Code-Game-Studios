using Dynasty.Core;
using NUnit.Framework;

namespace Dynasty.Tests.ProjectSetup
{
    public class ProjectSetupPlayModeTest
    {
        [Test]
        public void test_project_setup_play_mode_assembly_resolves_dynasty_core()
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
