using System.Text;
using Terrace.MasterData.Builder.Cli;

Console.OutputEncoding = Encoding.UTF8;
return BuildCommand.Run(args, Console.Out);
