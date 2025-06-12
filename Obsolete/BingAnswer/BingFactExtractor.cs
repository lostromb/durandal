using Durandal.API.Utils;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Durandal.Answers.StandardAnswers.Plugins.Bing
{
    public class BingFactExtractor
    {
        public static BingAnswerResponse ExtractBingFacts(string query, string locale, ILogger logger)
        {
            try
            {
                string lookupUrl = "http://www.bing.com/search?q=" + HttpUtility.UrlEncode(query);
                logger.Log("Looking up Bing facts for " + query, LogLevel.Vrb);
                WebClient client = new WebClient();
                client.Encoding = Encoding.UTF8;
                client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:51.0) Gecko/20100101 Firefox/51.0");
                client.Headers.Add(HttpRequestHeader.Host, "www.bing.com");
                client.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.5");
                //client.Headers.Add(HttpRequestHeader.Cookie, "DUP=Q=qVLQfUWCg7nIrnFD1cynXw2&T=290542439&A=1&IG=368AB76D4E1A428A834952785980410D; SRCHUID=V=2&GUID=6A9D5B5F1168487BB6F7889853661512; MUIDB=2F9C7DD64ACD6B2B0CEE756F4B1E6ADD; SRCHD=AF=NOFORM; SRCHUSR=DOB=20160212; _EDGE_V=1; MUID=2F9C7DD64ACD6B2B0CEE756F4B1E6ADD; SRCHHPGUSR=CW=1903&CH=583&DPR=1&UTC=-420; _RwBf=s=10&o=0&A=AE8108EA4AC1FB5D467FD4B9FFFFFFFF; BFBUSR=BAWV=1&BAWSSO=4&HUSC=2&USSTS=1469815180258&BAWTZ=420&UBSC=7&USBSTS=1470165440440&USFSTS=1469632005562; KievRPSAuth=FABaARRaTOJILtFsMkpLVWSG6AN6C/svRwNmAAAEgAAACCR7%2BVPcLA7%2BGAFhO89NdU23HruK/WEAMhPzC5WfQs%2BWS/Hz7v4oCQcZH7oN1mKSy4tnVtkAYkd6my44TH7WmIakDr1/qWhI84XqNAuvUK2VWQwVxFzr6iwbL4KdmaD8UL%2BUFiwEcGuBmWIoXGpBwFJ2oLvunOOhAJTTkzBPf7Rnpyx6EBlY6e49PTIrZQ/GZpTJzR8niHFizPy6fGvPSc8FbeLXSJdx5f8%2BAkThKoIw2Df9Kd/z5OQIbAEjCnimXtRPhSWorznW5OXQN8eUC4tgRsIeoiibuyWu7nOyyOVsUxAlcWCgV7kuth5cMOG9P4NpUeeKJKdpI/I6gcOSDArli4wD0bxtfShOhoRtf56%2BCfsbX5nKFbCNXUa0AYzPw3NyFACvBF/2qJyWdcZBuu08nCs6%2BUrr4Q%3D%3D; PPLState=1; ANON=A=AE8108EA4AC1FB5D467FD4B9FFFFFFFF&E=135e&W=1; NAP=V=1.9&E=1313&C=ZLu7NlOMbSzo2_25OwmPhnE418zVwuntTRt07xB38ThhCRWen-L72w&W=1; _IFAV=COUNT=0; OptInV8=optIn; _ITAB=STAB=REC; _IREC=SEETEXT=1; WLID=5oMO8LtvEMflpQNC5f3GeUnOxZD+p2uSpu1y9jxDEs1A7fWnyR3mzZAfQWmBBN7XXhEszAZwT/aSYyLdqA1V5nWJ6a5bxzc8AzPNnB7B+2Q=; _U=1eura69V-bqI2pEaPgXBnZkjlZebEtnr44qVRedxlQ97iYSI29e9Z6eF1JfFzESC69jV5M9VNUn-YuV43f59QrnAB3KeaoTLQBBSc9UNeiOFePlkVSYYSNjNW8MvpYS7D; _SS=PC=MOZO&SID=28204F47B9CE674736994502B81D6649&R=4149&HV=1489688041; SRCHS=PC=MOZO; _EDGE_S=mkt=en-us&SID=28204F47B9CE674736994502B81D6649; ipv6=hit=1");
                
                string resultBlob = client.DownloadString(lookupUrl);

                // Try and extract top/pole answer div
                Regex poleAnswerRipper = new Regex("<li class=\"b_ans b_top[\\w\\W]{5000}");
                string poleAnswerString = DurandalUtils.RegexRip(poleAnswerRipper, resultBlob, 0);

                if (string.IsNullOrEmpty(poleAnswerString))
                {
                    logger.Log("No pole answer found.", LogLevel.Vrb);
                    return null;
                }

                // First, detect dictionary answer because this will mess us up
                if (poleAnswerString.Contains("WordContainer"))
                {
                    logger.Log("The pole answer was Dictionary so I will ignore it", LogLevel.Std);
                    return null;
                }

                BingAnswerResponse factResponse = ParseAnswerType1(poleAnswerString);
                if (factResponse == null)
                {
                    factResponse = ParseAnswerType2(poleAnswerString);
                }
                if (factResponse == null)
                {
                    factResponse = ParseAnswerType3(poleAnswerString);
                }
                if (factResponse == null)
                {
                    factResponse = ParseAnswerType4(poleAnswerString);
                }
                if (factResponse == null)
                {
                    factResponse = ParseAnswerType5(poleAnswerString);
                }
                if (factResponse == null)
                {
                    factResponse = ParseAnswerType6(poleAnswerString);
                }
                if (factResponse == null)
                {
                    factResponse = ParseAnswerFallback(poleAnswerString);
                }

                if (factResponse == null)
                {
                    logger.Log("A pole answer was found, but no existing parser was able to parse it", LogLevel.Std);
                }
                else
                {
                    logger.Log("A pole answer was successfully parsed; returning it as a bing answer response", LogLevel.Std);
                    // Hackish - clean up the text a bit
                    factResponse.Body = PrettifyAnswerString(factResponse.Body, logger);
                    if (!string.IsNullOrEmpty(factResponse.Footer))
                    {
                        factResponse.Footer = DurandalUtils.RegexRemove(new Regex("[\\(\\)]"), factResponse.Footer);
                    }
                }

                return factResponse;
            }
            catch (Exception e)
            {
                logger.Log("An exception occurred while retriving Bing facts: " + e.Message);
            }

            return null;
        }

        /// <summary>
        /// Hackish function that cleans up common formatting stuff that the raw Bing answer gives us (Distill answer is the biggest culprit).
        /// Removes excess text from long answers, cleans up unicode and whitespace issues
        /// </summary>
        /// <param name="input"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static string PrettifyAnswerString(string input, ILogger logger)
        {
            // Remove unicode control chars that leak through
            StringBuilder nonControlCharBuf = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (!char.IsControl(c))
                {
                    nonControlCharBuf.Append(c);
                }
            }

            input = nonControlCharBuf.ToString();

            if (input.Length > 50)
            {
                // Remove extra parentheses blocks from long text answer runs
                logger.Log("The answer is longer that 50 chars so I am trimming out parenthesized blocks", LogLevel.Std);
                Regex parenthesesRipper = new Regex(" \\(.+?\\)");
                input = DurandalUtils.RegexRemove(parenthesesRipper, input);
            }

            int trimmedSentenceLength = 0;
            Regex sentenceMatcher = new Regex("(.+?\\w[\\.\\?\\!])(?=\\s|$)");
            MatchCollection matches = sentenceMatcher.Matches(input);
            foreach (Match match in matches)
            {
                // Keep or drop sentences based on these rules:
                // Always keep at least 1 sentence
                if (trimmedSentenceLength == 0)
                {
                    trimmedSentenceLength = match.Index + match.Length;
                }
                // Keep all sentences where the total response length is less than 160 characters
                // Drop sentences containing ellipses
                // FIXME the sentence matcher doesn't care about ellipses so that does nothing
                else if (trimmedSentenceLength + match.Length < 160 &&
                    !match.Value.Contains("..."))
                {
                    trimmedSentenceLength = match.Index + match.Length;
                }
                else
                {
                    break;
                }
            }

            if (trimmedSentenceLength > 0)
            {
                input = input.Substring(0, trimmedSentenceLength);
            }

            // Remove duplicate whitespace
            Regex whitespaceCleaner = new Regex("\\s\\s+");
            input = DurandalUtils.RegexReplace(whitespaceCleaner, input, " ").Trim();

            logger.Log("After prettification the answer string is " + input, LogLevel.Std);

            return input;
        }

        /// <summary>
        /// Sanitizes text that came from HTML. Unescapes the characters and removes all tags.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string Sanitize(string input)
        {
            if (input == null)
            {
                return null;
            }
            
            Regex tagExtractor = new Regex("<.+?>");
            return HttpUtility.HtmlDecode(DurandalUtils.RegexReplace(tagExtractor, input, string.Empty)).Trim();
        }

        private static BingAnswerResponse ParseAnswerType1(string poleAnswerString)
        {
            // "How tall is Michael Jordan"
            Regex answerMatcher = new Regex("<div[^>]+b_focusTextLarge[^>]+>(.+?)</div>");
            Regex labelMatcher = new Regex("<div[^>]+b_focusLabel[^>]+>(.+?)</div>");
            Regex footerMatcher = new Regex("<li[^>]+b_secondaryFocus[^>]+>(.+?)</li>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString, 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));
            string footer = Sanitize(DurandalUtils.RegexRip(footerMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = footer,
            };
        }

        private static BingAnswerResponse ParseAnswerType2(string poleAnswerString)
        {
            // "How old is Bill Nye"
            Regex answerMatcher = new Regex("<div[^>]+b_focusTextMedium[^>]+>(.+?)</div>");
            Regex labelMatcher = new Regex("<div[^>]+b_focusLabel[^>]+>(.+?)</div>");
            Regex footerMatcher = new Regex("<li[^>]+b_secondaryFocus[^>]+>(.+?)</li>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString, 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));
            string footer = Sanitize(DurandalUtils.RegexRip(footerMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = footer,
            };
        }

        private static BingAnswerResponse ParseAnswerType3(string poleAnswerString)
        {
            //
            Regex answerMatcher = new Regex("<div[^>]+b_focusTextSmall[^>]+>(.+?)</div>");
            Regex labelMatcher = new Regex("<div[^>]+b_focusLabel[^>]+>(.+?)</div>");
            Regex footerMatcher = new Regex("<li[^>]+b_secondaryFocus[^>]+>(.+?)</li>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString, 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));
            string footer = Sanitize(DurandalUtils.RegexRip(footerMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = footer,
            };
        }

        private static BingAnswerResponse ParseAnswerType4(string poleAnswerString)
        {
            // "who is benedict arnold"
            Regex answerMatcher = new Regex("<div[^>]+rwrl rwrl_(?:sec|pri) rwrl_padref[^>]+>(.+?)</div>");
            Regex labelMatcher = new Regex("<div[^>]+b_focusLabel[^>]+>(.+?)</div>");
            Regex footerMatcher = new Regex("<li[^>]+b_secondaryFocus[^>]+>(.+?)</li>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString, 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));
            string footer = Sanitize(DurandalUtils.RegexRip(footerMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = footer,
            };
        }
        
        private static BingAnswerResponse ParseAnswerType5(string poleAnswerString)
        {
            // "Tom cruise's wife"
            Regex answerMatcher = new Regex("<div[^>]+b_secondaryFocus[^>]+>(.+?)</div>");
            Regex labelMatcher = new Regex("<h2.+?>(.+?)</h2>");
            Regex footerMatcher = new Regex("<li >(.+?){1,30}</li>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString, 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));
            string footer = Sanitize(DurandalUtils.RegexRip(footerMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = footer,
            };
        }

        private static BingAnswerResponse ParseAnswerType6(string poleAnswerString)
        {
            // "what is the sphinx"
            Regex answerMatcher = new Regex("<div[^>]+b_bgdesc[^>]+>(.+?)</div>");
            Regex labelMatcher = new Regex("<h2[^>]+b_entityTitle[^>]+>(.+?)</h2>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString, 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = string.Empty,
            };
        }

        // This answer is really inaccurate
        private static BingAnswerResponse ParseAnswerFallback(string poleAnswerString)
        {
            // "who is the president of Japan"
            Regex answerMatcher = new Regex("<p>(.+?)</p>");
            Regex labelMatcher = new Regex("<div class=\"b_focusLabel.*?>(.+?)</div>");
            Regex footerMatcher = new Regex("<li class=\"b_secondaryFocus\">(.+?)</li>");
            string bingAnswer = Sanitize(DurandalUtils.RegexRip(answerMatcher, poleAnswerString.Substring(0, 1500), 1));
            string label = Sanitize(DurandalUtils.RegexRip(labelMatcher, poleAnswerString, 1));
            string footer = Sanitize(DurandalUtils.RegexRip(footerMatcher, poleAnswerString, 1));

            if (string.IsNullOrEmpty(bingAnswer))
            {
                return null;
            }

            if (bingAnswer.Contains("SafeSearch"))
            {
                return null;
            }

            return new BingAnswerResponse()
            {
                Header = label,
                Body = bingAnswer,
                Footer = footer,
            };
        }
    }
}
