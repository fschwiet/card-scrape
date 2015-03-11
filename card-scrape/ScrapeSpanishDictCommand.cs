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
	public class ScrapeSpanishDictCommand : ConsoleCommand
	{
		public string TargetDirectory;
		public List<InputVerb> Verbs = new List<InputVerb>();

		public ScrapeSpanishDictCommand ()
		{
			this.IsCommand ("scrape-sd");
			this.AllowsAnyAdditionalArguments ("<targetDirectory> <verb|filename>+ where filename is a file containing a list of verbs");
		}

		public override int? OverrideAfterHandlingArgumentsBeforeRun (string[] remainingArguments)
		{
			if (remainingArguments.Length < 1)
				throw new ConsoleHelpAsException ("No output directory specified.");

			TargetDirectory = Path.GetFullPath(remainingArguments [0]);

			if (!Directory.Exists (TargetDirectory)) {
				Console.WriteLine ("Creating directory: " + TargetDirectory);

				Directory.CreateDirectory (TargetDirectory);
			}

			Verbs = InputVerb.ParseVerbOptions (remainingArguments.Skip(1));

			if (!Verbs.Any ())
				throw new ConsoleHelpAsException ("No verbs specified.");

			return base.OverrideAfterHandlingArgumentsBeforeRun (remainingArguments);
		}

		public override int Run (string[] remainingArguments)
		{
			var options = new ChromeOptions();
			var service = ChromeDriverService.CreateDefaultService();
			service.SuppressInitialDiagnosticInformation = true;

			var driver = new ChromeDriver (service, options);

			foreach (var inputVerb in Verbs) {

				driver.Navigate ().GoToUrl ("http://spanishdict.com/translate/" + inputVerb.Verb);

				var definitionFileTarget = Path.Combine (TargetDirectory, inputVerb.Verb + ".definition.txt");
				File.WriteAllText (definitionFileTarget, driver.PageSource);

				var conjugationLink = driver.FindElementsByLinkText ("Conjugation").FirstOrDefault ();

				if (conjugationLink != null) {
					var conjugationFileTarget = Path.Combine (TargetDirectory, inputVerb.Verb + ".conjugation.txt");
					File.WriteAllText (conjugationFileTarget, driver.PageSource);
				}
			}

			return -1;
		}
	}
}

