using System;
using OpenQA.Selenium.Remote;
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

			return definition;
		}
	}
}

