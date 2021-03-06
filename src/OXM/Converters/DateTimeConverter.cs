using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OXM
{
    public class DateTimeConverter : SimpleTypeConverter<DateTimeOffset>
    {
        private const string p = @"^(?<Sign>[-|+]?)(?<Year>\d{4})-(?<Month>\d{1,2})-(?<Day>\d{1,2})T(?<Hour>\d{2}):(?<Minute>\d{2}):(?<Second>\d{2})(?<Ticks>(?:\.\d+)?)(?<Z>Z)?(?:(?<ZoneSign>[+-])(?<ZoneHour>\d{2}):(?<ZoneMinute>\d{2}))?$";
        static Regex pattern = new Regex(p, RegexOptions.Compiled);

        public override bool TrySerialize(DateTimeOffset value, out string s)
        {
            s = value.Offset.Ticks == 0 ?
                value.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF") : value.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK");
            return true;
        }

        public override bool TryParse(string value, out DateTimeOffset obj)
        {
            var match = pattern.Match(value);
            if (!match.Success)
            {
                obj = new DateTimeOffset();
                return false;
            }
            int year = int.Parse(match.Groups["Year"].Value);
            int month = int.Parse(match.Groups["Month"].Value);
            int day = int.Parse(match.Groups["Day"].Value);
            int hour = int.Parse(match.Groups["Hour"].Value);
            int minute = int.Parse(match.Groups["Minute"].Value);
            int second = int.Parse(match.Groups["Second"].Value);

            string ticks = match.Groups["Ticks"].Value;
            int millisecond = 0;
            if (ticks != "")
                millisecond = int.Parse(ticks.Substring(1));
            TimeSpan offset = ParseTimeOffset(match);
            obj = new DateTimeOffset(year, month, day, hour, minute, second, millisecond, offset);
            return true;
        }

        public static TimeSpan ParseTimeOffset(Match match)
        {
            var offset = new TimeSpan();
            if (match.Groups["ZoneSign"].Success)
            {
                int hours = int.Parse(match.Groups["ZoneHour"].Value);
                int minutes = int.Parse(match.Groups["ZoneMinute"].Value);

                if (match.Groups["ZoneSign"].Value == "-")
                {
                    hours *= -1;
                }
                offset = new TimeSpan(hours, minutes, 0);
            }

            return offset;
        }
    }

    public class NullableDateTimeConverter : NullabeConverter<DateTimeOffset>
    {
        DateTimeConverter _converter = new DateTimeConverter();
        protected override SimpleTypeConverter<DateTimeOffset> Converter
        {
            get
            {
                return _converter;
            }
        }
    }
}
