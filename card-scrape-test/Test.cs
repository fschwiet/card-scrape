using NUnit.Framework;
using System;
using cardscrape;
using OpenQA.Selenium.Chrome;

namespace cardscrapetest
{
	[TestFixture ()]
	public class Test
	{
		ChromeDriver driver;

		[TestFixtureSetUp]
		public void Setup() {
			driver = new ChromeDriver ();
		}

		[TestFixtureTearDown]
		public void TearDown() {
			driver.Dispose ();
		}
			
		[Test ()]
		[TestCase("yo pongo", "I put")]
		[TestCase("ellos ponen", "they put")]
		public void CanTranslateEllosPonen (string input, string expectedOutput)
		{
			var result = TranslateUtils.TranslateSpanishToEnglish (driver, input);

			Assert.AreEqual (expectedOutput, result);
		}
	}
}

