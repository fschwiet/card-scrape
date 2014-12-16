using System;

namespace cardscrape
{
	class MainClass
	{
		public static int Main (string[] args)
		{
			try{
				var commands = ManyConsole.ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(typeof(MainClass));
				return ManyConsole.ConsoleCommandDispatcher.DispatchCommand (commands, args, Console.Out);
			}
			catch(Exception e) {
				Console.Error.WriteLine (e.ToString ());
				return -1;
			}
		}
	}
}
