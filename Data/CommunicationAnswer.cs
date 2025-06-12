using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Collections.Generic;
using System.IO;
using Durandal.API;
using TimeExpressions;
using TimeExpressions.Enums;

namespace CommunicationAnswer
{
    public class CommunicationAnswer : Answer
    {
        private enum InfoField
        {
            Address,
            PhoneNumber,
            LastName
        }

        private IDictionary<string, InfoField> contactInfoFields;
        
        public CommunicationAnswer()
            : base("communication")
        {
            contactInfoFields = new Dictionary<string, InfoField>();
            contactInfoFields.Add("address", InfoField.Address);
            contactInfoFields.Add("last name", InfoField.LastName);
            contactInfoFields.Add("phone number", InfoField.PhoneNumber);
            contactInfoFields.Add("phone num", InfoField.PhoneNumber);
            contactInfoFields.Add("phone #", InfoField.PhoneNumber);
            contactInfoFields.Add("number", InfoField.PhoneNumber);
        }

        protected override ConversationTree BuildConversationTree()
        {
            ConversationTree returnVal = new ConversationTree(Domain);
            returnVal.AddRootNode(new ConversationNode(
                "user_absolute_location_query", AbsoluteLocationQuery, DomainScope.Local));
            returnVal.AddRootNode(new ConversationNode(
                "user_relative_location_query", RelativeLocationQuery, DomainScope.Local));
            returnVal.AddRootNode(new ConversationNode(
                "calendar_time_query", CalendarTimeQuery, DomainScope.Local));
            returnVal.AddRootNode(new ConversationNode(
                "calendar_place_query", CalendarPlaceQuery, DomainScope.Local));
            returnVal.AddRootNode(new ConversationNode(
                "calendar_appointment_request", CalendarApptRequest, DomainScope.Local));
            returnVal.AddRootNode(new ConversationNode(
                "contact_info_query", ContactInfoQuery, DomainScope.Local));
            return returnVal;
        }

        public DialogResult AbsoluteLocationQuery(LUData luData, ObjectStore store)
        {
            return new DialogResult()
            {
                MultiTurnResult = MultiTurnBehavior.None,
                ResponseCode = Result.Success,
                ResponseSSML = "I am near 148th Ave and Main Street in Bellevue"
            };
        }

        public DialogResult RelativeLocationQuery(LUData luData, ObjectStore store)
        {
            string location = TryGetSlotValue(luData.Result.TaggedText, "relative_location");
            if (string.IsNullOrWhiteSpace(location))
            {
                return new DialogResult(Result.Skip);
            }

            int time = new Random().Next(5, 30);
            return new DialogResult()
            {
                MultiTurnResult = MultiTurnBehavior.None,
                ResponseCode = Result.Success,
                ResponseSSML = "I should be at " + location + " in " + time + " minutes."
            };
        }

        public DialogResult CalendarTimeQuery(LUData luData, ObjectStore store)
        {
            string eventName = TryGetSlotValue(luData.Result.TaggedText, "event_title");
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return new DialogResult(Result.Skip);
            }
            // Capitalize the event
            eventName = char.ToUpper(eventName[0]) + eventName.Substring(1);
            
            return new DialogResult()
            {
                MultiTurnResult = MultiTurnBehavior.None,
                ResponseCode = Result.Success,
                ResponseSSML = eventName + " is at 8:00 P.M."
            };
        }

        public DialogResult CalendarPlaceQuery(LUData luData, ObjectStore store)
        {
            string eventName = TryGetSlotValue(luData.Result.TaggedText, "event_title");
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return new DialogResult(Result.Skip);
            }
            // Capitalize the event
            eventName = char.ToUpper(eventName[0]) + eventName.Substring(1);
            
            return new DialogResult()
            {
                MultiTurnResult = MultiTurnBehavior.None,
                ResponseCode = Result.Success,
                ResponseSSML = eventName + " is at Diamond's house"
            };
        }

        public DialogResult CalendarApptRequest(LUData luData, ObjectStore store)
        {
            SlotValue requestTime = TryGetSlot(luData.Result.TaggedText, "request_time");
            if (requestTime == null)
            {
                return new DialogResult(Result.Skip);
            }

            // Try and extract the time
            Context newTimexContext = new Context()
            {
                Normalization = Normalization.Future,
                OriginalText = luData.Result.TaggedText.Utterance.OriginalText,
                StartIndex = 0,
                TemporalType = TemporalType.Date | TemporalType.Time,
                UseInference = true,
                WeekdayLogicType = WeekdayLogic.SimpleOffset,
                ReferenceDateTime = DateTime.Now
            };

            IList<TimexMatch> timexMatches = requestTime.GetTimeMatches(TemporalType.Date | TemporalType.Time, CultureInfo.CurrentCulture, newTimexContext);
            
            if (timexMatches.Count == 0)
            {
                return new DialogResult(Result.Skip);
            }
            ExtendedDateTime time = timexMatches[0].ExtendedDateTime;
            
            if (time.TemporalType == TemporalType.Date)
            {
                // No time was suggested. Look for a good time in the user's calendar.
                return new DialogResult()
                {
                    MultiTurnResult = MultiTurnBehavior.None,
                    ResponseCode = Result.Success,
                    ResponseSSML = "Yes, I am free at " + time.FormatValue() + ". Sound good?"
                };
            }
            else
            {
                // A time was suggested. Match against the current calendar and see if that slot will work.
                return new DialogResult()
                {
                    MultiTurnResult = MultiTurnBehavior.None,
                    ResponseCode = Result.Success,
                    ResponseSSML = "Yes, " + time.FormatValue() + " will work for me"
                };
            }
        }

        public DialogResult ContactInfoQuery(LUData luData, ObjectStore store)
        {
            string contactName = TryGetSlotValue(luData.Result.TaggedText, "contact");
            if (string.IsNullOrWhiteSpace(contactName))
            {
                return new DialogResult(Result.Skip);
            }

            string requestField = TryGetSlotValue(luData.Result.TaggedText, "query_field");
            if (string.IsNullOrWhiteSpace(requestField))
            {
                return new DialogResult(Result.Skip);
            }

            float coercionConfidence = 0.0f;
            InfoField requestType = DurandalUtils.RewriteSlotValue(requestField, contactInfoFields, out coercionConfidence);
            if (coercionConfidence < 0.4)
            {
                return new DialogResult(Result.Skip);
            }

            switch (requestType)
            {
                case InfoField.PhoneNumber:
                    return new DialogResult()
                    {
                        MultiTurnResult = MultiTurnBehavior.None,
                        ResponseCode = Result.Success,
                        ResponseSSML = contactName + " phone number is 555-5555"
                    };

                case InfoField.Address:
                    return new DialogResult()
                    {
                        MultiTurnResult = MultiTurnBehavior.None,
                        ResponseCode = Result.Success,
                        ResponseSSML = contactName + " address is 123 Fake Street"
                    };

                case InfoField.LastName:
                    return new DialogResult()
                    {
                        MultiTurnResult = MultiTurnBehavior.None,
                        ResponseCode = Result.Success,
                        ResponseSSML = contactName + " last name is Reginald"
                    };
            }

            return new DialogResult(Result.Failure);
        }
    }
}
