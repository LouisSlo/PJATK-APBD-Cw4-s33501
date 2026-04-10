using System;

namespace LegacyRenewalApp
{
    // --- 1. REGUŁY ZNIŻEK ---
    public class DiscountResult
    {
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public interface IDiscountRule
    {
        DiscountResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, decimal baseAmount, bool useLoyaltyPoints);
    }

    public class SegmentDiscountRule : IDiscountRule
    {
        public DiscountResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, decimal baseAmount, bool useLoyaltyPoints)
        {
            if (customer.Segment == "Silver") return new DiscountResult { Amount = baseAmount * 0.05m, Note = "silver discount; " };
            if (customer.Segment == "Gold") return new DiscountResult { Amount = baseAmount * 0.10m, Note = "gold discount; " };
            if (customer.Segment == "Platinum") return new DiscountResult { Amount = baseAmount * 0.15m, Note = "platinum discount; " };
            if (customer.Segment == "Education" && plan.IsEducationEligible) return new DiscountResult { Amount = baseAmount * 0.20m, Note = "education discount; " };
            return new DiscountResult();
        }
    }

    public class LoyaltyTimeDiscountRule : IDiscountRule
    {
        public DiscountResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, decimal baseAmount, bool useLoyaltyPoints)
        {
            if (customer.YearsWithCompany >= 5) return new DiscountResult { Amount = baseAmount * 0.07m, Note = "long-term loyalty discount; " };
            if (customer.YearsWithCompany >= 2) return new DiscountResult { Amount = baseAmount * 0.03m, Note = "basic loyalty discount; " };
            return new DiscountResult();
        }
    }

    public class TeamSizeDiscountRule : IDiscountRule
    {
        public DiscountResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, decimal baseAmount, bool useLoyaltyPoints)
        {
            if (seatCount >= 50) return new DiscountResult { Amount = baseAmount * 0.12m, Note = "large team discount; " };
            if (seatCount >= 20) return new DiscountResult { Amount = baseAmount * 0.08m, Note = "medium team discount; " };
            if (seatCount >= 10) return new DiscountResult { Amount = baseAmount * 0.04m, Note = "small team discount; " };
            return new DiscountResult();
        }
    }

    public class LoyaltyPointsDiscountRule : IDiscountRule
    {
        public DiscountResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, decimal baseAmount, bool useLoyaltyPoints)
        {
            if (useLoyaltyPoints && customer.LoyaltyPoints > 0)
            {
                int pointsToUse = customer.LoyaltyPoints > 200 ? 200 : customer.LoyaltyPoints;
                return new DiscountResult { Amount = pointsToUse, Note = $"loyalty points used: {pointsToUse}; " };
            }
            return new DiscountResult();
        }
    }

    // --- 2. OPŁATY ZA WSPARCIE ---
    public interface ISupportFeeCalculator
    {
        (decimal fee, string note) Calculate(string planCode, bool includePremiumSupport);
    }

    public class PremiumSupportFeeCalculator : ISupportFeeCalculator
    {
        public (decimal fee, string note) Calculate(string planCode, bool includePremiumSupport)
        {
            if (!includePremiumSupport) return (0m, string.Empty);
            
            return planCode switch
            {
                "START" => (250m, "premium support included; "),
                "PRO" => (400m, "premium support included; "),
                "ENTERPRISE" => (700m, "premium support included; "),
                _ => (0m, string.Empty)
            };
        }
    }

    // --- 3. OPŁATY ZA PŁATNOŚĆ ---
    public interface IPaymentFeeStrategy
    {
        bool AppliesTo(string paymentMethod);
        (decimal fee, string note) Calculate(decimal amountBeforeFee);
    }

    public class CardPaymentStrategy : IPaymentFeeStrategy { public bool AppliesTo(string method) => method == "CARD"; public (decimal, string) Calculate(decimal a) => (a * 0.02m, "card payment fee; "); }
    public class BankTransferStrategy : IPaymentFeeStrategy { public bool AppliesTo(string method) => method == "BANK_TRANSFER"; public (decimal, string) Calculate(decimal a) => (a * 0.01m, "bank transfer fee; "); }
    public class PaypalStrategy : IPaymentFeeStrategy { public bool AppliesTo(string method) => method == "PAYPAL"; public (decimal, string) Calculate(decimal a) => (a * 0.035m, "paypal fee; "); }
    public class InvoicePaymentStrategy : IPaymentFeeStrategy { public bool AppliesTo(string method) => method == "INVOICE"; public (decimal, string) Calculate(decimal a) => (0m, "invoice payment; "); }

    // --- 4. PODATKI ---
    public interface ITaxStrategy
    {
        bool AppliesTo(string country);
        decimal GetTaxRate();
    }

    public class PolandTaxStrategy : ITaxStrategy { public bool AppliesTo(string c) => c == "Poland"; public decimal GetTaxRate() => 0.23m; }
    public class GermanyTaxStrategy : ITaxStrategy { public bool AppliesTo(string c) => c == "Germany"; public decimal GetTaxRate() => 0.19m; }
    public class CzechRepublicTaxStrategy : ITaxStrategy { public bool AppliesTo(string c) => c == "Czech Republic"; public decimal GetTaxRate() => 0.21m; }
    public class NorwayTaxStrategy : ITaxStrategy { public bool AppliesTo(string c) => c == "Norway"; public decimal GetTaxRate() => 0.25m; }
    public class DefaultTaxStrategy : ITaxStrategy { public bool AppliesTo(string c) => true; public decimal GetTaxRate() => 0.20m; }
}
