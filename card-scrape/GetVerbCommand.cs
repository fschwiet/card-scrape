﻿using System;
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
			public string ConjugationType;
		}

		public List<InputVerb> Verbs = new List<InputVerb>();
		public string[] NounsToSkip = new [] {
			"vosotros"
		};
		public string[] TensesToInclude = new [] { 
			"present"
		};

		public bool ShouldValidateOnly = false;

		public GetVerbCommand ()
		{
			this.IsCommand ("get-verb", "Generates notes for a Spanish verb");
			this.HasAdditionalArguments (null, " <verb|filename>+ where filename is a file containing a list of verbs");
			this.HasOption ("validate", "Only validate the input verbs", v => ShouldValidateOnly = true);
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

			var driver = new ChromeDriver (service, options);

			driver.Manage ().Timeouts ().ImplicitlyWait (TranslateUtils.LongWait);

			foreach (var inputVerb in Verbs) {

				List<Result> results = null;

				var retriesLeft = 3;

				while (true) {
				
					try {
						results = LookupResults (driver, inputVerb, ShouldValidateOnly);
						break;
					} 
					catch(Exception e) {
						if (retriesLeft-- == 0) {
							throw;
						}

						driver.Close ();
						driver = new ChromeDriver (service, options);

						Console.Error.WriteLine ("Retrying: " + inputVerb.Verb);
						Console.Error.WriteLine ("Exception was: " + e.Message);
					}
				}

				using (var csvWriter = new CsvHelper.CsvWriter (Console.Out))
					foreach (var result in results) {
						csvWriter.WriteField (result.Term);
						csvWriter.WriteField (result.TermDefinition);
						var tags = result.ConjugationType + " ";
						if (result.InfinitiveForm != null) {
							csvWriter.WriteField (result.InfinitiveForm + " - " + result.InfinitiveDefinition);
							tags = tags + "infinitive:" + result.InfinitiveForm;
						} else {
							csvWriter.WriteField ("");
							tags = tags + "infinitive:" + result.Term;
						}
						csvWriter.WriteField (tags);
						csvWriter.NextRecord ();
					}
			}

			driver.Close ();

			return 0;
		}

		private List<Result> LookupResults(RemoteWebDriver driver, InputVerb inputVerb, bool validateOnly) {
		
			driver.Navigate ().GoToUrl ("http://www.spanishdict.com/translate/" + inputVerb.Verb);

			var infinitiveTranslationDiv = driver.FindElementsByCssSelector (".card .quickdef .lang").FirstOrDefault ();
			var conjugateLink = driver.FindElementsByCssSelector (".card a[href^='http://www.spanishdict.com/conjugate']").FirstOrDefault ();

			if (infinitiveTranslationDiv == null) {
				throw new ConsoleHelpAsException ("Unable to find translation of '" + inputVerb.Verb + "'.");
			}

			string infinitiveTranslation = infinitiveTranslationDiv.Text.Trim();

			if (conjugateLink == null) {
				throw new ConsoleHelpAsException ("Is '" + inputVerb.Verb + "' a verb?  Unable to find conjugation link.");
			}

			// for some undetermined reason conjugateLink is reported as not visible for verb 'ir'
			// so we can't just conjugateLink.Click (), instead we use javascript
			driver.ExecuteScript ("arguments[0].click();", conjugateLink);

			var term = driver.FindElementByCssSelector (".card .quickdef .source-text").Text.Trim ();

			driver.Manage ().Timeouts ().ImplicitlyWait (TranslateUtils.ShortWait);

			//  I forget why we prefer the translation on the conjugatio page...
			infinitiveTranslationDiv = driver.FindElementsByCssSelector (".card .quickdef .lang").FirstOrDefault();

			if (infinitiveTranslationDiv != null)
				infinitiveTranslation = infinitiveTranslationDiv.Text.Trim();
			
			driver.Manage ().Timeouts ().ImplicitlyWait (TranslateUtils.LongWait);

			if (inputVerb.ExtraInfo != null) {
				infinitiveTranslation = infinitiveTranslation + " " + inputVerb.ExtraInfo;
			}

			if (term.ToLowerInvariant () != inputVerb.Verb.ToLower ()) {
				throw new ConsoleHelpAsException ("The verb provided '" + inputVerb.Verb + "' does not match the base form found '" + term + "'.  Please use the base form.");
			}

			if (validateOnly)
				return new List<Result> ();

			foreach (var vtableLabel in driver.FindElementsByCssSelector ("a.vtable-label")) {
				var vtableType = vtableLabel.Text.Trim ().ToLower ().Replace (" ", "-");
				driver.ExecuteScript ("arguments[0].classList.add('vtable-label-" + vtableType + "');", vtableLabel);
			}

			var infinitiveResult = new Result () {
				Term = term,
				TermDefinition = infinitiveTranslation,
				ConjugationType = "conjugation:infinitive"
			};

			List<Result> results = new List<Result> ();

			results.Add (infinitiveResult);

			var indicativeTable = driver.FindElementByCssSelector (".vtable-label-indicative + .vtable-wrapper");

			var columnNames = indicativeTable.FindElements (By.CssSelector ("tr:first-child td")).Select (e => e.Text.Trim().ToLower ()).Skip (1).ToArray ();
			var rowNames = indicativeTable.FindElements (By.CssSelector ("tr td:first-child")).Select (e => e.Text.Trim().ToLower ()).Skip (1).ToArray ();

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

					var result = new Result () {
						TermConjugationIdentifier = identifier,
						InfinitiveForm = term,
						InfinitiveDefinition = infinitiveTranslation,
						Term = value,
						DeambiguatingNounphrase = nounPhrase,
						ConjugationType = "conjugation:indicative-" + columnNames [column].ToLower()
					};

					results.Add (result);
				}

				foreach (var result in results.Where(r => r.TermDefinition == null)) {

					result.TermDefinition = TranslateUtils.TranslateSpanishToEnglish (driver, result.DeambiguatingNounphrase + " " + result.Term);

					if (inputVerb.ExtraInfo != null) {
						result.TermDefinition = result.TermDefinition + " " + inputVerb.ExtraInfo;
					}
				}
			}

			return results;
		}
	}
}

