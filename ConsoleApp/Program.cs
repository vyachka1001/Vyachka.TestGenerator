using System;
using Core.TestGenerator.Implementations;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var generator = new NUnitTestGenerator(10, 10, 10);
            generator.GenerateTestsAsync(@"d:\STUDYING\3_course\СПП\Labs\Vyachka.TestGenerator\ConsoleApp\input",
                @"d:\STUDYING\3_course\СПП\Labs\Vyachka.TestGenerator\ConsoleApp\output").Wait();
        }
    }
}