using NUnit.Framework;
using System;
using cardscrape;
using OpenQA.Selenium.Chrome;

namespace cardscrapetest
{
	[TestFixture ()]
	public class Test
	{
		[Test ()]
		public void CanTranslateEllosPonen ()
		{
			using (var driver = new ChromeDriver ()) {
				var result = TranslateUtils.TranslateSpanishToEnglish (driver, "ellos ponen");

				Assert.AreEqual ("they put", result);
			}
		}
	}
}

