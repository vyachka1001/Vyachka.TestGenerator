using System.Threading.Tasks;

namespace Core.TestGenerator.Interfaces
{
    public interface ITestGenerator
    {
        Task GenerateTestsAsync(string inputDirectory, string outputDirectory);
    }
}