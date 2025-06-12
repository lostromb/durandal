/*
    ChatterBotAPI
    Copyright (C) 2011 pierredavidbelanger@gmail.com
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
namespace Durandal.Answers.StandardAnswers.Smalltalk {
    using System.Collections.Generic;

    class Cleverbot: ChatterBot {
		private readonly string url;
		private readonly int endIndex;
		
		public Cleverbot(string url, int endIndex) {
			this.url = url;
			this.endIndex = endIndex;
		}
		
		public ChatterBotSession CreateSession() {
			return new CleverbotSession(this.url, this.endIndex);
		}
	}
	
	class CleverbotSession: ChatterBotSession {
		private readonly string url;
		private readonly int endIndex;
		private readonly IDictionary<string, string> vars;
		
		public CleverbotSession(string url, int endIndex) {
			this.url = url;
			this.endIndex = endIndex;
			this.vars = new Dictionary<string, string>();
			this.vars["start"] = "y";
			this.vars["icognoid"] = "wsf";
			this.vars["fno"] = "0";
			this.vars["sub"] = "Say";
			this.vars["islearning"] = "1";
			this.vars["cleanslate"] = "false";
		}
		
		public ChatterBotThought Think(ChatterBotThought thought) {
			this.vars["stimulus"] = thought.Text;
			
			string formData = Utils.ParametersToWWWFormURLEncoded(this.vars);
			string formDataToDigest = formData.Substring(9, this.endIndex);
			string formDataDigest = Utils.MD5(formDataToDigest);
			this.vars["icognocheck"] = formDataDigest;
			
			string response = Utils.Post(this.url, this.vars);
			
			string[] responseValues = response.Split('\r');
			
			//vars[""] = Utils.StringAtIndex(responseValues, 0); ??
			this.vars["sessionid"] = Utils.StringAtIndex(responseValues, 1);
			this.vars["logurl"] = Utils.StringAtIndex(responseValues, 2);
			this.vars["vText8"] = Utils.StringAtIndex(responseValues, 3);
			this.vars["vText7"] = Utils.StringAtIndex(responseValues, 4);
			this.vars["vText6"] = Utils.StringAtIndex(responseValues, 5);
			this.vars["vText5"] = Utils.StringAtIndex(responseValues, 6);
			this.vars["vText4"] = Utils.StringAtIndex(responseValues, 7);
			this.vars["vText3"] = Utils.StringAtIndex(responseValues, 8);
			this.vars["vText2"] = Utils.StringAtIndex(responseValues, 9);
			this.vars["prevref"] = Utils.StringAtIndex(responseValues, 10);
			//vars[""] = Utils.StringAtIndex(responseValues, 11); ??
			this.vars["emotionalhistory"] = Utils.StringAtIndex(responseValues, 12);
			this.vars["ttsLocMP3"] = Utils.StringAtIndex(responseValues, 13);
			this.vars["ttsLocTXT"] = Utils.StringAtIndex(responseValues, 14);
			this.vars["ttsLocTXT3"] = Utils.StringAtIndex(responseValues, 15);
			this.vars["ttsText"] = Utils.StringAtIndex(responseValues, 16);
			this.vars["lineRef"] = Utils.StringAtIndex(responseValues, 17);
			this.vars["lineURL"] = Utils.StringAtIndex(responseValues, 18);
			this.vars["linePOST"] = Utils.StringAtIndex(responseValues, 19);
			this.vars["lineChoices"] = Utils.StringAtIndex(responseValues, 20);
			this.vars["lineChoicesAbbrev"] = Utils.StringAtIndex(responseValues, 21);
			this.vars["typingData"] = Utils.StringAtIndex(responseValues, 22);
			this.vars["divert"] = Utils.StringAtIndex(responseValues, 23);
			
			ChatterBotThought responseThought = new ChatterBotThought();
			
			responseThought.Text = Utils.StringAtIndex(responseValues, 16);
			
			return responseThought;
		}
		
		public string Think(string text) {
			return this.Think(new ChatterBotThought() { Text = text }).Text;
		}
	}
}