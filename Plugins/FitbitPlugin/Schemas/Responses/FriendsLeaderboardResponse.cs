﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class FriendsLeaderboardResponse
    {
        [JsonProperty("hideMeFromLeaderboard")]
        public bool HideMeFromLeaderboard { get; set; }

        [JsonProperty("friends")]
        public List<FriendLeaderboardEntry> Friends { get; set; }
    }

    //{
    //    "hideMeFromLeaderboard":"false",
    //    "friends":[
    //        {
    //            "average":
    //                {
    //                    "steps":9854,
    //                },
    //            "rank":
    //                {
    //                    "steps":1
    //                },
    //            "summary":
    //                {
    //                    "steps":56000
    //                },
    //            "lastUpdateTime":"2013-03-29T13:25:00",
    //            "user":
    //                {
    //                    "aboutMe":"I live in San Francisco.",
    //                    "avatar":"http://www.fitbit.com/images/profile/defaultProfile_100_male.gif",
    //                    "city":"San Francisco",
    //                    "country":"US",
    //                    "dateOfBirth":"1970-02-18",
    //                    "displayName":"Nick",
    //                    "encodedId":"257V3V",
    //                    "fullName":"Fitbit",
    //                    "gender":"MALE",
    //                    "height":176.7,
    //                    "offsetFromUTCMillis":-25200000,
    //                    "state":"CA",
    //                    "timezone":"America/Los_Angeles",
    //                    "weight":80.5
    //                }
    //         },
    //         {
    //            "average":
    //                {
    //                    "steps":13854,
    //                },
    //            "rank":
    //                {
    //                    "steps":2
    //                },
    //            "summary":
    //                {
    //                    "steps":45000
    //                },
    //            "lastUpdateTime":"2013-03-29T15:25:00",
    //            "user":
    //                {
    //                    "aboutMe":"",
    //                    "avatar":"http://www.fitbit.com/images/profile/defaultProfile_100_male.gif",
    //                    "city":"",
    //                    "country":"",
    //                    "dateOfBirth":"",
    //                    "displayName":"Fitbit U.",
    //                    "encodedId":"2246K9",
    //                    "fullName":"Fitbit User",
    //                    "gender":"NA",
    //                    "height":190.7,
    //                    "offsetFromUTCMillis":14400000,
    //                    "state":"",
    //                    "timezone":"Europe/Moscow",
    //                    "weight":0
    //                }
    //         }
    //     ]
    //}
}
