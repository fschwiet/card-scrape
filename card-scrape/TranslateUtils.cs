using System;
using OpenQA.Selenium.Remote;
using System.Collections.Generic;
using System.Linq;


namespace cardscrape
{
	public class TranslateUtils
	{
		static public readonly TimeSpan LongWait = TimeSpan.FromSeconds(20);
		static public readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(500);

		//  The translation has heuristics making it specific to NP+VP (see Normalize...)
		public static string TranslateSpanishToEnglish(RemoteWebDriver driver, string termToSearch) {

			var translateUrl = "http://www.spanishdict.com/translate/" + Uri.EscapeDataString (termToSearch);
			driver.Navigate ().GoToUrl (translateUrl);

			var translationPageTypeDisambiguator = driver.FindElementByCssSelector (".quickdef, .lang-tabs");

			if (translationPageTypeDisambiguator.GetAttribute("class").Contains("lang-tabs"))
				driver.FindElementById ("mt-to-en").Click ();

			string definition = null;

			do {
				//  Checking for an element that doesn't exist requires the fill timeout,
				//  so we're going to do some timeout switching.

				//  First we do a long search to be sure the page has had time to load whatever
				//  element we might be looking for, this search should still be fast as typically
				//  we find something.
				driver.FindElementsByCssSelector("#mt-en .mt-info.promt .mt-info-text, .quickdef .el");  // the selector is anything we can use

				//  Now we use the shorter timeout for the case where elements.Any() is typically
				//  false (as WebDriver will wait for the full time)
				driver.Manage().Timeouts().ImplicitlyWait(ShortWait);

				var externalEngineResults = driver.FindElementsByCssSelector("#mt-en .mt-info .mt-info-text");
				if (externalEngineResults.Any()) {

					// make sure all results have had a chance to load
					while(externalEngineResults.Count() < 3) {
						externalEngineResults = driver.FindElementsByCssSelector("#mt-en .mt-info .mt-info-text");
					}

					Dictionary<string,double> scores = new Dictionary<string, double>();

					foreach(var result in externalEngineResults.Select(e => NormalizeString(e.Text))) {

						if (!scores.ContainsKey(result)) {
							scores[result] = 0;
						}

						scores[result] += CheckPrefix(termToSearch, result) ? 1 : 0;
					}

					//  Give a slight preference to the PROMT result
					foreach(var element in driver.FindElementsByCssSelector("#mt-en .mt-info.promt .mt-info-text")) {

						scores[NormalizeString(element.Text)] += 0.1;
					}

					//  Give Google Translate half a vote
					var googleTranslation = GetGoogleTranslation(driver, termToSearch);

					if (scores.ContainsKey(googleTranslation))
						scores[googleTranslation] += 0.5;

					definition = scores.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).First();
				}
				else {
					var selector = ".quickdef .el";
					if (driver.FindElementsByCssSelector ("#translate-en").Any ()) {
						selector = " #translate-en .quickdef .el";
					}

					definition = driver.FindElementByCssSelector (selector).Text.Trim ().ToLowerInvariant ();

					driver.Manage().Timeouts().ImplicitlyWait(LongWait);
				}

			} while(definition.Length == 0);

			return definition;
		}

		//  The normalization behavior really only makes sense for NP+VP type phrases
		private static string NormalizeString(string input) {
			var result = input.Trim ().ToLowerInvariant ();

			// Make the initial "I" uppercase again
			if (result.StartsWith ("i "))
				result = "I " + result.Substring (2);

			if (result.StartsWith ("he(it) "))
				result = "he " + result.Substring ("he(it) ".Length);

			//  Trim initial "and " which google translate sometimes includes an initial "and"
			//  For example, "tú atraviesas" is translated as "and you go through"
			if (result.StartsWith ("and "))
				result = result.Substring (4);

			return result;
		}

		private static Dictionary<string,string> prefixMap = new Dictionary<string, string>() {
			{"yo", "I "},
			{"tú", "you "},
			{"él", "he "},
			{"nosotros", "we "},
			{"ellos", "they "}
		};

		private static bool CheckPrefix(string originalPhrase, string translatedPhrase) {
		
			var expectedPrefix = prefixMap [originalPhrase.Split (' ') [0]];

			return translatedPhrase.StartsWith (expectedPrefix);
		}

		private static string GetGoogleTranslation(RemoteWebDriver driver, string input) {
		
			driver.Navigate ().GoToUrl ("https://translate.google.com/#es/en/" + Uri.EscapeDataString (input));

			string result = null;

			do {
				if (result != null) {
					System.Threading.Thread.Sleep(200);
				}

				result = driver.FindElementByCssSelector ("#result_box").Text;
			} while(result.Contains("..."));

			return NormalizeString (result);
		}
	}
}

