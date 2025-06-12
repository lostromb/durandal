namespace Durandal.Common.Time.Timex.Enums
{
    /// <summary>
    /// Represents different logic rules to apply when resolving phrases that refer to days of the week.
    /// The most common case is "next monday" - in typical American usage this would refer the nearest Monday in the
    /// future that is more than, like, 3 days ahead.
    /// Other possible scenarios:
    /// the British English phrase "Tuesday week", which refers to the tuesday that falls in the next calendar week
    /// the Chinese equiv. of "next monday"  - it may follow the US-english interpretation, but perhaps with different minimum offset.
    /// This class is necessary to allow us to easily localize and configure this behavior.
    /// </summary>
    public enum WeekdayLogic
    {
        Programmatic,
        SimpleOffset,
        WeekBoundary
    }
}
