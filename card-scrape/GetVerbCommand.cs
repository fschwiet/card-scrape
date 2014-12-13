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
		public class InputVerb {
			public string Verb;
			public string ExtraInfo;
		}

		public class Result {
			public string TermConjugationIdentifier;
			public string DeambiguatingNounphrase;
			public string Term;
			public string TermDefinition;
			public string InfinitiveForm;
			public string InfinitiveDefinition;
		}

		public List<InputVerb> Verbs = new List<InputVerb>();
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
					foreach (var line in File.ReadAllLines (arg).Where (l => l.Trim ().Length > 0)) {
						var pieces = line.Split (new [] { ';' }, 2);
						Verbs.Add (new InputVerb() {
							Verb = pieces[0].Trim(),
							ExtraInfo = pieces.Length > 1 ? pieces[1].Trim() : null
						});
					}
				} else {
					Verbs.Add (new InputVerb() {
						Verb = arg
					});
				}
			}

			var options = new ChromeOptions();
			var service = ChromeDriverService.CreateDefaultService();
			service.SuppressInitialDiagnosticInformation = true;

			using (var driver = new ChromeDriver (service, options)) {

				driver.Manage ().Timeouts ().ImplicitlyWait (LongWait);

				foreach (var inputVerb in Verbs) {
				
					driver.Navigate ().GoToUrl ("http://www.spanishdict.com/translate/" + inputVerb.Verb);

					var translationDiv = driver.FindElementsByCssSelector (".card .quickdef .lang").FirstOrDefault ();
					var conjugateLink = driver.FindElementsByCssSelector (".card a[href^='http://www.spanishdict.com/conjugate']").FirstOrDefault ();

					if (translationDiv == null) {
						Console.WriteLine ("Unable to find translation of '" + inputVerb.Verb + "'.");
						return 1;
					}

					if (conjugateLink == null) {
						Console.WriteLine ("Is '" + inputVerb.Verb + "' a verb?  Unable to find conjugation link.");
						return 1;
					}

					// for some undetermined reason conjugateLink is reported as not visible for verb 'ir'
					// so we can't just conjugateLink.Click (), instead we use javascript
					driver.ExecuteScript ("arguments[0].click();", conjugateLink);

					var term = driver.FindElementByCssSelector (".card .quickdef .source-text").Text.Trim ();
					var translation = driver.FindElementByCssSelector (".card .quickdef .lang").Text.Trim ();

					if (inputVerb.ExtraInfo != null) {
						translation = translation + " " + inputVerb.ExtraInfo;
					}

					if (term.ToLowerInvariant () != inputVerb.Verb.ToLower ()) {
						Console.WriteLine ("The verb provided '" + inputVerb.Verb + "' does not match the base form found '" + term + "'.  Please use the base form.");
						return 1;
					}

					foreach (var vtableLabel in driver.FindElementsByCssSelector ("a.vtable-label")) {
						var vtableType = vtableLabel.Text.Trim ().ToLower ().Replace (" ", "-");
						driver.ExecuteScript ("arguments[0].classList.add('vtable-label-" + vtableType + "');", vtableLabel);
					}
					
					var infinitiveResult = new Result () {
						Term = term,
						TermDefinition = translation
					};

					List<Result> results = new List<Result> ();

					results.Add (infinitiveResult);

					var indicativeTable = driver.FindElementByCssSelector (".vtable-label-indicative + .vtable-wrapper");

					var columnNames = indicativeTable.FindElements (By.CssSelector ("tr:first-child td")).Select (e => e.Text.ToLower ()).Skip (1).ToArray ();
					var rowNames = indicativeTable.FindElements (By.CssSelector ("tr td:first-child")).Select (e => e.Text.ToLower ()).Skip (1).ToArray ();

					for (var column = 0; column < columnNames.Length; column++) {

						if (!TensesToInclude.Contains (columnNames [column]))
							continue;

						for (var row = 0; row < rowNames.Length; row++) {

							var selector = "tr:nth-child(" + (row + 2) + ") td:nth-child(" + (column + 2) + ")";
							var value = indicativeTable.FindElement (By.CssSelector (selector)).Text.Trim ().ToLower ();

							var identifier = String.Join (",", new [] {
								term,
								columnNames [column],
								rowNames [row],
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
								
						foreach (var result in results.Where(r => r.TermDefinition == null)) {

							var termToSearch = result.DeambiguatingNounphrase + " " + result.Term;

							var translateUrl = "http://www.spanishdict.com/translate/" + Uri.EscapeDataString (termToSearch);
							driver.Navigate ().GoToUrl (translateUrl);

							string definition = null;

							do {
								var selector = ".mt-info.promt .mt-info-text, .quickdef .el";

								//  Checking for an element that doesn't exist requires the fill timeout,
								//  so we're going to do some timeout switching.

								//  First we do a long search to be sure the page has had time to load whatever
								//  element we might be looking for, this search should still be fast as typically
								//  we find something.
								driver.FindElementByCssSelector(selector);

								//  Now we use the shorter timeout for the case where elements.Any() is typically
								//  false (as WebDriver will wait for the full time)
								driver.Manage().Timeouts().ImplicitlyWait(ShortWait);

								if (driver.FindElementsByCssSelector ("#translate-en").Any ()) {
									selector = ".mt-info.promt .mt-info-text, #translate-en .quickdef .el";
								}

								definition = driver.FindElementByCssSelector (selector).Text.Trim ().ToLowerInvariant ();

								driver.Manage().Timeouts().ImplicitlyWait(LongWait);
							} while(definition.Length == 0);

							result.TermDefinition = definition;					
						}
					}

					using (var csvWriter = new CsvHelper.CsvWriter (Console.Out))
						foreach (var result in results) {
							csvWriter.WriteField (result.Term);
							csvWriter.WriteField (result.TermDefinition);
							if (result.InfinitiveForm != null) {
								csvWriter.WriteField (result.InfinitiveForm + " - " + result.InfinitiveDefinition);
							} else {
								csvWriter.WriteField ("");
							}
							csvWriter.NextRecord ();
						}
				}
			}

			return 0;
		}

		private readonly TimeSpan LongWait = TimeSpan.FromSeconds(20);
		private readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(500);
	}
}

