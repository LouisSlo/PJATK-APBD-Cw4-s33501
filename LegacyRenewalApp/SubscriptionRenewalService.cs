using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LegacyRenewalApp
{
    public class SubscriptionRenewalService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ISubscriptionPlanRepository _planRepository;
        private readonly IBillingGateway _billingGateway;
        private readonly IEnumerable<IDiscountRule> _discountRules;
        private readonly IEnumerable<IPaymentFeeStrategy> _paymentFeeStrategies;
        private readonly IEnumerable<ITaxStrategy> _taxStrategies;
        private readonly ISupportFeeCalculator _supportFeeCalculator;

        public SubscriptionRenewalService() : this(
            new CustomerRepository(),
            new SubscriptionPlanRepository(),
            new BillingGatewayAdapter(),
            new List<IDiscountRule> { new SegmentDiscountRule(), new LoyaltyTimeDiscountRule(), new TeamSizeDiscountRule(), new LoyaltyPointsDiscountRule() },
            new List<IPaymentFeeStrategy> { new CardPaymentStrategy(), new BankTransferStrategy(), new PaypalStrategy(), new InvoicePaymentStrategy() },
            new List<ITaxStrategy> { new PolandTaxStrategy(), new GermanyTaxStrategy(), new CzechRepublicTaxStrategy(), new NorwayTaxStrategy(), new DefaultTaxStrategy() },
            new PremiumSupportFeeCalculator()
        ) { }

        public SubscriptionRenewalService(
            ICustomerRepository customerRepository,
            ISubscriptionPlanRepository planRepository,
            IBillingGateway billingGateway,
            IEnumerable<IDiscountRule> discountRules,
            IEnumerable<IPaymentFeeStrategy> paymentFeeStrategies,
            IEnumerable<ITaxStrategy> taxStrategies,
            ISupportFeeCalculator supportFeeCalculator)
        {
            _customerRepository = customerRepository;
            _planRepository = planRepository;
            _billingGateway = billingGateway;
            _discountRules = discountRules;
            _paymentFeeStrategies = paymentFeeStrategies;
            _taxStrategies = taxStrategies;
            _supportFeeCalculator = supportFeeCalculator;
        }

        public RenewalInvoice CreateRenewalInvoice(
            int customerId,
            string planCode,
            int seatCount,
            string paymentMethod,
            bool includePremiumSupport,
            bool useLoyaltyPoints)
        {
            ValidateInputs(customerId, planCode, seatCount, paymentMethod);

            string normalizedPlanCode = planCode.Trim().ToUpperInvariant();
            string normalizedPaymentMethod = paymentMethod.Trim().ToUpperInvariant();

            var customer = _customerRepository.GetById(customerId);
            var plan = _planRepository.GetByCode(normalizedPlanCode);

            if (!customer.IsActive)
                throw new InvalidOperationException("Inactive customers cannot renew subscriptions");

            var notesBuilder = new StringBuilder();
            decimal baseAmount = (plan.MonthlyPricePerSeat * seatCount * 12m) + plan.SetupFee;

            decimal discountAmount = CalculateDiscounts(customer, plan, seatCount, baseAmount, useLoyaltyPoints, notesBuilder);
            decimal subtotalAfterDiscount = baseAmount - discountAmount;

            if (subtotalAfterDiscount < 300m)
            {
                subtotalAfterDiscount = 300m;
                notesBuilder.Append("minimum discounted subtotal applied; ");
            }

            var (supportFee, supportNote) = _supportFeeCalculator.Calculate(normalizedPlanCode, includePremiumSupport);
            notesBuilder.Append(supportNote);

            var paymentStrategy = _paymentFeeStrategies.FirstOrDefault(s => s.AppliesTo(normalizedPaymentMethod))
                                  ?? throw new ArgumentException("Unsupported payment method");
            
            var (paymentFee, paymentNote) = paymentStrategy.Calculate(subtotalAfterDiscount + supportFee);
            notesBuilder.Append(paymentNote);

            var taxStrategy = _taxStrategies.FirstOrDefault(s => s.AppliesTo(customer.Country)) ?? new DefaultTaxStrategy();
            decimal taxBase = subtotalAfterDiscount + supportFee + paymentFee;
            decimal taxAmount = taxBase * taxStrategy.GetTaxRate();
            
            decimal finalAmount = taxBase + taxAmount;
            if (finalAmount < 500m)
            {
                finalAmount = 500m;
                notesBuilder.Append("minimum invoice amount applied; ");
            }
            var invoice = BuildInvoice(customerId, normalizedPlanCode, normalizedPaymentMethod, seatCount,
                                       baseAmount, discountAmount, supportFee, paymentFee, taxAmount, finalAmount,
                                       notesBuilder.ToString().Trim(), customer.FullName);

            _billingGateway.SaveInvoice(invoice);
            SendEmailNotification(customer, normalizedPlanCode, invoice.FinalAmount);

            return invoice;
        }
        private void ValidateInputs(int customerId, string planCode, int seatCount, string paymentMethod)
        {
            if (customerId <= 0) throw new ArgumentException("Customer id must be positive");
            if (string.IsNullOrWhiteSpace(planCode)) throw new ArgumentException("Plan code is required");
            if (seatCount <= 0) throw new ArgumentException("Seat count must be positive");
            if (string.IsNullOrWhiteSpace(paymentMethod)) throw new ArgumentException("Payment method is required");
        }

        private decimal CalculateDiscounts(Customer customer, SubscriptionPlan plan, int seatCount, decimal baseAmount, bool useLoyaltyPoints, StringBuilder notesBuilder)
        {
            decimal totalDiscount = 0m;
            foreach (var rule in _discountRules)
            {
                var result = rule.Calculate(customer, plan, seatCount, baseAmount, useLoyaltyPoints);
                totalDiscount += result.Amount;
                notesBuilder.Append(result.Note);
            }
            return totalDiscount;
        }

        private RenewalInvoice BuildInvoice(int customerId, string planCode, string paymentMethod, int seatCount,
            decimal baseAmount, decimal discountAmount, decimal supportFee, decimal paymentFee, decimal taxAmount,
            decimal finalAmount, string notes, string customerName)
        {
            return new RenewalInvoice
            {
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{customerId}-{planCode}",
                CustomerName = customerName,
                PlanCode = planCode,
                PaymentMethod = paymentMethod,
                SeatCount = seatCount,
                BaseAmount = Math.Round(baseAmount, 2, MidpointRounding.AwayFromZero),
                DiscountAmount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero),
                SupportFee = Math.Round(supportFee, 2, MidpointRounding.AwayFromZero),
                PaymentFee = Math.Round(paymentFee, 2, MidpointRounding.AwayFromZero),
                TaxAmount = Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero),
                FinalAmount = Math.Round(finalAmount, 2, MidpointRounding.AwayFromZero),
                Notes = notes,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private void SendEmailNotification(Customer customer, string planCode, decimal finalAmount)
        {
            if (string.IsNullOrWhiteSpace(customer.Email)) return;

            string subject = "Subscription renewal invoice";
            string body = $"Hello {customer.FullName}, your renewal for plan {planCode} has been prepared. Final amount: {finalAmount:F2}.";
            
            _billingGateway.SendEmail(customer.Email, subject, body);
        }
    }
}
