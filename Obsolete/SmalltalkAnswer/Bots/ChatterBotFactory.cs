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
    using System;

    public class ChatterBotFactory {
		
		public ChatterBot Create(ChatterBotType type) {
			return this.Create(type, null);
		}

		public ChatterBot Create(ChatterBotType type, object arg) {
			switch (type) {
			case ChatterBotType.CLEVERBOT:
				return new Cleverbot("http://www.cleverbot.com/webservicemin", 26);
			case ChatterBotType.JABBERWACKY:
				return new Cleverbot("http://jabberwacky.com/webservicemin", 20);
			case ChatterBotType.PANDORABOTS:
				if (arg == null) throw new ApplicationException("PANDORABOTS needs a botid arg");
				return new Pandorabots(arg.ToString());
			}
			return null;
		}
	}
}