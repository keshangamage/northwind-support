using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed class SlaPolicy
{
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];
    public double PremiumCustomerMultiplier { get; set; }
    public double MinimumWindowHours { get; set; }
    public double AtRiskThresholdPercent { get; set; }

    public TimeSpan GetWindow(TicketPriority priority, CustomerTier tier)
    {
        var hours = BaseWindowHours[priority];
        if (tier == CustomerTier.Premium)
        {
            hours = Math.Max(hours * PremiumCustomerMultiplier, MinimumWindowHours);
        }

        return TimeSpan.FromSeconds(Math.Ceiling(hours * 3600));
    }

    public void Validate()
    {
        if (Enum.GetValues<TicketPriority>().Any(priority =>
                !BaseWindowHours.TryGetValue(priority, out var hours)
                || !double.IsFinite(hours) || hours <= 0 || hours * 3600 > int.MaxValue)
            || !double.IsFinite(PremiumCustomerMultiplier)
            || PremiumCustomerMultiplier <= 0 || PremiumCustomerMultiplier > 1
            || !double.IsFinite(MinimumWindowHours)
            || MinimumWindowHours <= 0 || MinimumWindowHours * 3600 > int.MaxValue
            || !double.IsFinite(AtRiskThresholdPercent)
            || AtRiskThresholdPercent < 0 || AtRiskThresholdPercent > 100)
        {
            throw new InvalidOperationException("The Sla configuration is invalid.");
        }
    }
}
