namespace PilotsRUs.Shared.SDK.ScheduleTemplates;

public static class ScheduleFrequencyExtensions
{
    public static int ToIntervalDays(this ScheduleFrequency frequency) => frequency switch
    {
        ScheduleFrequency.Daily => 1,
        ScheduleFrequency.EverySecondDay => 2,
        ScheduleFrequency.EveryThirdDay => 3,
        ScheduleFrequency.EveryFourthDay => 4,
        ScheduleFrequency.EveryFifthDay => 5,
        ScheduleFrequency.EverySixthDay => 6,
        ScheduleFrequency.Weekly => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency))
    };
}
