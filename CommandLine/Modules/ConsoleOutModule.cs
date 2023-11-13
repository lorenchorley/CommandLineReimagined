using Microsoft.Extensions.DependencyInjection;
using System;
using UIComponents;

namespace CommandLine.Modules
{
    public class ConsoleOutModule
    {
        private readonly IServiceProvider _serviceProvider;

        public ConsoleOutModule(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public CliBlock StartBlock(string description) // Gérer avec un container de ligne pour que ça perd pas sa place dans la console et pour que tout soit regroupé (si souhaité)
        {
            return _serviceProvider.GetRequiredService<CliBlock>().SetDesciption(description);
        }

    }
}
