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

		public static string TranslateSpanishToEnglish(RemoteWebDriver driver, string termToSearch) {

			var translateUrl = "http://www.spanishdict.com/translate/" + Uri.EscapeDataString (termToSearch);
			driver.Navigate ().GoToUrl (translateUrl);

			string definition = null;

			do {
				//  Checking for an element that doesn't exist requires the fill timeout,
				//  so we're going to do some timeout switching.

				//  First we do a long search to be sure the page has had time to load whatever
				//  element we might be looking for, this search should still be fast as typically
				//  we find something.
				driver.FindElementsByCssSelector(".mt-info.promt .mt-info-text, .quickdef .el");  // the selector is anything we can use

				//  Now we use the shorter timeout for the case where elements.Any() is typically
				//  false (as WebDriver will wait for the full time)
				driver.Manage().Timeouts().ImplicitlyWait(ShortWait);

				var externalEngineResults = driver.FindElementsByCssSelector(".mt-info .mt-info-text");
				if (externalEngineResults.Any()) {

					// make sure all results have had a chance to load
					while(externalEngineResults.Count() < 3) {
						externalEngineResults = driver.FindElementsByCssSelector(".mt-info .mt-info-text");
					}

					Dictionary<string,double> scores = new Dictionary<string, double>();

					foreach(var result in externalEngineResults.Select(e => NormalizeString(e.Text))) {

						if (!scores.ContainsKey(result)) {
							scores[result] = 0;
						}

						scores[result] += 1;
					}

					//  Give a slight preference to the PROMT result
					foreach(var element in driver.FindElementsByCssSelector(".mt-info.promt .mt-info-text")) {

						scores[NormalizeString(element.Text)] += 0.1;
					}

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

		private static string NormalizeString(string input) {
			return input.Trim ().ToLowerInvariant ();
		}
	}
}

