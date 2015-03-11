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
	public class InputVerb {

		public string Verb;
		public string ExtraInfo;

		public override string ToString ()
		{
			return Verb;
		}

		public static List<InputVerb> ParseVerbOptions(IEnumerable<string> commandLineArguments) {

			var verbArguments = new List<string> ();

			foreach (var arg in commandLineArguments) {
				if (File.Exists (arg)) {
					foreach (var line in File.ReadAllLines (arg).Where (l => l.Trim ().Length > 0)) {
						verbArguments.Add (line);
					}
				} else {
					verbArguments.Add (arg);
				}
			}

			var results = new List<InputVerb> ();

			foreach (var verbArgument in verbArguments) {
				var pieces = verbArgument.Split (new [] { ';' }, 2);
				results.Add (new InputVerb() {
					Verb = pieces[0].Trim(),
					ExtraInfo = pieces.Length > 1 ? pieces[1].Trim() : null
				});
			}

			return results;
		}
	}
	
}
