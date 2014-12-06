using System;
using ManyConsole;

namespace cardscrape
{
	public class GetVerbCommand : ConsoleCommand
	{
		public string Verb;

		public GetVerbCommand ()
		{
			this.IsCommand ("get-verb", "Generates notes for a Spanish verb");
			this.HasAdditionalArguments (1, " <verb>");
		}

		public override int? OverrideAfterHandlingArgumentsBeforeRun (string[] remainingArguments)
		{
			Verb = remainingArguments [0];

			return base.OverrideAfterHandlingArgumentsBeforeRun (remainingArguments);
		}

		public override int Run (string[] remainingArguments)
		{
			return 0;
		}
	}
}

