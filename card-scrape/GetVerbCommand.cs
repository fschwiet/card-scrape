using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
			public string DeambiguatingNounphrase;
			public string Term;
			public string TermDefinition;  // use google translate?
			public string InfinitiveForm;
			public string InfinitiveDefinition;
		}

		public List<string> Verbs = new List<string>();
		public string[] NounsToSkip = new [] {
			"vosotros"
		};
		public string[] TensesToInclude = new [] { 
			"present"
		};

		public GetVerbCommand ()
		{
			this.IsCommand ("get-verb", "Generates notes for a Spanish verb");
			this.HasAdditionalArguments (null, " <verb|filename>+ where filename is a file containing a list of verbs");
			this.SkipsCommandSummaryBeforeRunning ();
		}

		public override int Run (string[] remainingArguments)
		{
			foreach (var arg in remainingArguments) {
				if (File.Exists (arg)) {
					Verbs.AddRange (File.ReadAllLines (arg).Where (l => l.Trim ().Length > 0));
				} else {
					Verbs.Add (arg);
				}
			}

			var options = new ChromeOptions();
			var service = ChromeDriverService.CreateDefaultService();
			service.SuppressInitialDiagnosticInformation = true;

			using (var driver = new ChromeDriver(service, options))
			foreach (var verb in Verbs) {
			
				driver.Navigate ().GoToUrl ("http://www.spanishdict.com/translate/" + verb);

				var translationDiv = driver.FindElementsByCssSelector (".card .quickdef .lang").FirstOrDefault();
				var conjugateLink = driver.FindElementsByCssSelector (".card a[href^='http://www.spanishdict.com/conjugate']").FirstOrDefault();

				if (translationDiv == null) {
					Console.WriteLine ("Unable to find translation of '" + verb + "'.");
					return 1;
				}

				if (conjugateLink == null) {
					Console.WriteLine ("Is '" + verb + "' a verb?  Unable to find conjugation link.");
					return 1;
				}

				// for some undetermined reason conjugateLink is reported as not visible for verb 'ir'
				// so we can't just conjugateLink.Click (), instead we use javascript
				driver.ExecuteScript ("arguments[0].click();", conjugateLink);

				var term = driver.FindElementByCssSelector (".card .quickdef .source-text").Text.Trim ();
				var translation = driver.FindElementByCssSelector (".card .quickdef .lang").Text.Trim();

				if (term.ToLowerInvariant () != verb.ToLower ()) {
					Console.WriteLine ("The verb provided '" + verb + "' does not match the base form found '" + term + "'.  Please use the base form.");
					return 1;
				}

				foreach (var vtableLabel in driver.FindElementsByCssSelector ("a.vtable-label")) {
					var vtableType = vtableLabel.Text.Trim ().ToLower().Replace(" ", "-");
					driver.ExecuteScript("arguments[0].classList.add('vtable-label-" + vtableType + "');",vtableLabel);
				}

				var indicativeTable = driver.FindElementByCssSelector (".vtable-label-indicative + .vtable-wrapper");

				var columnNames = indicativeTable.FindElements (By.CssSelector ("tr:first-child td")).Select (e => e.Text.ToLower()).Skip (1).ToArray ();
				var rowNames = indicativeTable.FindElements (By.CssSelector ("tr td:first-child")).Select (e => e.Text.ToLower()).Skip (1).ToArray ();

				List<Result> results = new List<Result> ();

				for (var column = 0; column < columnNames.Length; column++) {

					if (!TensesToInclude.Contains(columnNames[column]))
						continue;

					for (var row = 0; row < rowNames.Length; row++) {

						var selector = "tr:nth-child(" + (row + 2) + ") td:nth-child(" + (column + 2) + ")";
						var value = indicativeTable.FindElement (By.CssSelector (selector)).Text.Trim().ToLower();

						var identifier = String.Join (",", new [] {
							term,
							columnNames[column],
							rowNames[row],
							});

						var nounPhrase = rowNames [row].Split ('/').First ();

						if (NounsToSkip.Contains (nounPhrase)) {
							continue;
						}

						results.Add (new Result () {
							TermConjugationIdentifier = identifier,
							InfinitiveForm = term,
							InfinitiveDefinition = translation,
							Term = value,
							DeambiguatingNounphrase = nounPhrase
						});
					}

					foreach (var result in results) {

						var termToSearch = result.Term;

						if (result.DeambiguatingNounphrase != null) {
							//  For some reason Google Translate says "nosotros dormimos" is "us slept" and
							//  that "dormimos" is "we slept".  So lets not put Nosotros in.

							//  "tú estás" is being weird too.  So we only add the nownphrase for ambigious
							//  conjugations
							termToSearch = result.DeambiguatingNounphrase + " " + result.Term;
						}

						driver.Navigate ().GoToUrl ("https://translate.google.com/#es/en/" + termToSearch);

						string definition = null;

						do {
							if (definition != null) {
								System.Threading.Thread.Sleep(200);
							}

							definition = driver.FindElementByCssSelector ("#result_box").Text.Trim ();
						} while(definition.Contains("..."));

						result.TermDefinition = definition;
					}
				}

				using (var csvWriter = new CsvHelper.CsvWriter(Console.Out))
				foreach (var result in results) {
						csvWriter.WriteField (result.Term);
						csvWriter.WriteField (result.TermDefinition);
						csvWriter.WriteField (result.InfinitiveForm + " - " + result.InfinitiveDefinition);
						csvWriter.NextRecord ();
				}
			}

			return 0;
		}
	}
}

