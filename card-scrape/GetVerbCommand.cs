using System;
using System.Collections.Generic;
using System.Linq;
using ManyConsole;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;

namespace cardscrape
{
	public class GetVerbCommand : ConsoleCommand
	{
		public class Result {
			public string TermConjugationIdentifier;
			public string Term;
			public string TermDefinition;  // use google translate?
			public string InfinitiveForm;
			public string InfinitiveDefinition;
		}

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

				foreach (var vtableLabel in driver.FindElementsByCssSelector ("a.vtable-label")) {
					var vtableType = vtableLabel.Text.Trim ().ToLower().Replace(" ", "-");
					driver.ExecuteScript("arguments[0].classList.add('vtable-label-" + vtableType + "');",vtableLabel);
				}

				//var indicativeLabel = driver.FindElementByCssSelector ("a.vtable-label:contains('Indicative')");
				var indicativeTable = driver.FindElementByCssSelector (".vtable-label-indicative + .vtable-wrapper");

				var columnNames = indicativeTable.FindElements (By.CssSelector ("tr:first-child td")).Select (e => e.Text.ToLower()).Skip (1).ToArray ();
				var rowNames = indicativeTable.FindElements (By.CssSelector ("tr td:first-child")).Select (e => e.Text.ToLower()).Skip (1).ToArray ();

				for (var column = 0; column < columnNames.Length; column++) {

					if (columnNames [column] != "present")
						continue;

					for (var row = 0; row < rowNames.Length; row++) {

						var selector = "tr:nth-child(" + (row + 2) + ") td:nth-child(" + (column + 2) + ")";
						var value = indicativeTable.FindElement (By.CssSelector (selector)).Text.Trim().ToLower();
						Console.WriteLine (String.Format ("{0}, {1}, {2}, {3}", selector, columnNames [column], rowNames [row], value));
					}
				}
				Console.WriteLine (String.Join (" ", columnNames));
				Console.WriteLine (String.Join (" ", rowNames));

				var result = new Result ();
				result.InfinitiveForm = term;
				result.InfinitiveDefinition = translation;

				System.Threading.Thread.Sleep (5000);
			}

			return 0;
		}
	}
}

