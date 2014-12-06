using System;
using System.Collections.Generic;
using System.Linq;
using ManyConsole;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;

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
			var options = new ChromeOptions();
			var service = ChromeDriverService.CreateDefaultService();
			service.SuppressInitialDiagnosticInformation = true;

			using(var driver = new ChromeDriver(service, options)) {

				driver.Navigate ().GoToUrl ("http://www.spanishdict.com/translate/" + Verb);

				var translationDiv = driver.FindElementsByCssSelector (".card .quickdef .lang").FirstOrDefault();
				var conjugateLink = driver.FindElementsByCssSelector (".card a[href^='http://www.spanishdict.com/conjugate']").FirstOrDefault();

				if (translationDiv == null) {
					Console.WriteLine ("Unable to find translation of '" + Verb + "'.");
					return 1;
				}

				if (conjugateLink == null) {
					Console.WriteLine ("Is '" + Verb + "' a verb?  Unable to find conjugation link.");
					return 1;
				}

				conjugateLink.Click ();

				var term = driver.FindElementByCssSelector (".card .quickdef .source-text").Text.Trim ();
				var translation = driver.FindElementByCssSelector (".card .quickdef .lang").Text.Trim();

				if (term.ToLowerInvariant () != Verb.ToLower ()) {
					Console.WriteLine ("The verb provided '" + Verb + "' does not match the base form found '" + term + "'.  Please use the base form.");
					return 1;
				}

				Console.WriteLine (term + " - " + translation);

				System.Threading.Thread.Sleep (5000);
			}

			return 0;
		}
	}
}

